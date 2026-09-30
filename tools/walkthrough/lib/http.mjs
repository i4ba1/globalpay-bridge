import crypto from 'node:crypto';

export const settings = {
  api: (process.env.GPAY_API ?? 'http://localhost:5080').replace(/\/+$/, ''),
  tokenUrl:
    process.env.GPAY_TOKEN_URL ??
    'http://localhost:8080/realms/globalpay/protocol/openid-connect/token',
  clientId: process.env.GPAY_CLIENT_ID ?? 'globalpay-cli',
  userPassword: process.env.GPAY_PASSWORD ?? 'Password123!',
  webhookSecret: process.env.GPAY_WEBHOOK_SECRET ?? 'local-dev-webhook-secret',
};

export function sha256Hex(bytes) {
  return crypto.createHash('sha256').update(bytes).digest('hex');
}

export async function getToken(username) {
  const body = new URLSearchParams({
    grant_type: 'password',
    client_id: settings.clientId,
    username,
    password: settings.userPassword,
  });

  const response = await fetch(settings.tokenUrl, { method: 'POST', body });

  if (!response.ok) {
    throw new Error(`token request for '${username}' failed: ${response.status} ${await response.text()}`);
  }

  return (await response.json()).access_token;
}

/**
 * Direct API call, used for polling and for machine-to-machine steps that have no UI surface.
 * Pass `rawBody` when the exact bytes matter (webhook signature).
 */
export async function callApi(method, path, { token, body, rawBody, headers = {} } = {}) {
  const requestHeaders = { accept: 'application/json', ...headers };

  if (token) {
    requestHeaders.authorization = `Bearer ${token}`;
  }

  const hasBody = body !== undefined || rawBody !== undefined;

  if (hasBody && !requestHeaders['content-type']) {
    requestHeaders['content-type'] = 'application/json';
  }

  const response = await fetch(`${settings.api}${path}`, {
    method,
    headers: requestHeaders,
    body: rawBody !== undefined ? rawBody : body === undefined ? undefined : JSON.stringify(body),
  });

  const text = await response.text();
  let json = null;

  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = null;
  }

  return { status: response.status, json, text };
}

/** Uploads bytes to a blob using the SAS URL returned by POST /timesheet-imports. */
export async function putBlob(uploadUri, bytes) {
  const response = await fetch(uploadUri, {
    method: 'PUT',
    headers: {
      'x-ms-blob-type': 'BlockBlob',
      'content-type': 'text/csv',
    },
    body: bytes,
  });

  if (!response.ok) {
    throw new Error(`blob upload failed: ${response.status} ${await response.text()}`);
  }

  return response.status;
}

export function signWebhook(rawBody, timestamp) {
  return crypto
    .createHmac('sha256', settings.webhookSecret)
    .update(`${timestamp}.${rawBody}`, 'utf8')
    .digest('hex');
}

/** Posts a signed provider webhook. The provider is external, so this is machine-to-machine. */
export async function postWebhook({ eventType, paymentReference, amount, currency, occurredAt }) {
  const rawBody = JSON.stringify({ eventType, paymentReference, amount, currency, occurredAt });
  const timestamp = Math.floor(Date.now() / 1000);

  return callApi('POST', '/api/v1/webhooks/payment-provider', {
    rawBody,
    headers: {
      'content-type': 'application/json',
      'X-Provider-Event-Id': `evt-walkthrough-${crypto.randomUUID()}`,
      'X-Provider-Timestamp': String(timestamp),
      'X-Provider-Signature': signWebhook(rawBody, timestamp),
    },
  });
}

export async function waitFor(label, probe, { timeoutMs = 120000, intervalMs = 3000 } = {}) {
  const deadline = Date.now() + timeoutMs;
  let last;

  while (Date.now() < deadline) {
    last = await probe();

    if (last && last.done) {
      return last.value;
    }

    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }

  throw new Error(`timed out waiting for ${label}; last observation: ${JSON.stringify(last)}`);
}

/** Blocks until the API reports ready, so a walkthrough does not race a container restart. */
export async function waitForApiReady({ timeoutMs = 120000, intervalMs = 2000 } = {}) {
  const deadline = Date.now() + timeoutMs;
  let lastStatus = 'no response';

  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${settings.api}/health/ready`, { headers: { accept: 'text/plain' } });
      lastStatus = `${response.status} ${(await response.text()).trim()}`;

      if (response.status === 200) {
        return true;
      }
    } catch (error) {
      lastStatus = error.message;
    }

    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }

  throw new Error(`API did not become ready within ${timeoutMs} ms; last status: ${lastStatus}`);
}
