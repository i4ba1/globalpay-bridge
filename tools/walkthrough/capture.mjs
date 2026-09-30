import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';
import { SwaggerSession, VIEWPORT, CLIP } from './lib/swagger.mjs';
import {
  callApi,
  getToken,
  postWebhook,
  putBlob,
  settings,
  sha256Hex,
  waitFor,
  waitForApiReady,
} from './lib/http.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../..');
const gifDir = path.join(root, 'docs/walkthrough/gifs');
const artifactDir = path.join(root, 'docs/walkthrough/artifacts');

const scenario = JSON.parse(fs.readFileSync(path.join(here, 'testdata/scenario.json'), 'utf8'));

const marker = process.env.GPAY_RUN ?? String(Date.now()).slice(-4);
const substitute = (template) => template.replaceAll('{{RUN}}', marker);

const csvBytes = buildCsv();
const csvName = `timesheets-2026-09-${marker}.csv`;
const csvSha = sha256Hex(csvBytes);

function buildCsv() {
  const refs = new Map(scenario.workers.map((worker) => [worker.key, substitute(worker.externalRef)]));
  const lines = ['workerRef,workDate,units,componentCode'];

  for (const row of scenario.timesheet) {
    lines.push([refs.get(row.worker), row.workDate, row.units, row.componentCode ?? ''].join(','));
  }

  return Buffer.from(`${lines.join('\n')}\n`, 'utf8');
}

function assertEqual(actual, expected, label) {
  if (actual !== expected) {
    throw new Error(`${label}: expected ${expected} but got ${actual}`);
  }
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

fs.mkdirSync(gifDir, { recursive: true });
fs.mkdirSync(artifactDir, { recursive: true });
fs.writeFileSync(path.join(artifactDir, csvName), csvBytes);

const state = {};
const log = (...args) => console.log(...args);

/** Expands an operation, drives it through the UI, and asserts the intercepted HTTP status. */
async function captureOperation(session, { operationId, token, parameters = {}, body, expect }) {
  if (token) {
    const dialog = await session.openAuthorizeModal(token);
    await session.confirmAuthorize(dialog);
  }

  const block = await session.expandOperation(operationId);
  await session.frame(block);

  await session.ensureEditable(block);
  await session.frame(block);

  for (const [name, value] of Object.entries(parameters)) {
    await session.fillParameter(block, name, value);
  }

  if (body !== undefined) {
    await session.fillBody(block, body);
  }

  await session.frame(block);

  await session.execute(block, expect);
  await session.frame(block);

  const response = session.lastResponse(expect.method, expect.path);
  assert(
    response,
    `no intercepted response for ${expect.method} ${expect.path} (${session.describeFailures()})`,
  );

  const actualStatus = response.status;

  if (Array.isArray(expect.status)) {
    assert(
      expect.status.includes(actualStatus),
      `${expect.method} ${expect.path} returned ${actualStatus}, expected one of ${expect.status}: ${JSON.stringify(response.json)?.slice(0, 240)}`,
    );
  } else {
    assertEqual(
      actualStatus,
      expect.status,
      `${expect.method} ${expect.path} -> ${JSON.stringify(response.json)?.slice(0, 240)}`,
    );
  }

  log(`  ${expect.method} ${expect.path} -> ${actualStatus}`);

  return response;
}

async function main() {
  log(`GlobalPay Bridge walkthrough`);
  log(`  api        : ${settings.api}`);
  log(`  run marker : ${marker}  (worker refs W-${marker}-001/002)`);
  log(`  csv        : ${csvName}  sha256=${csvSha.slice(0, 16)}...`);
  log('');

  await waitForApiReady();
  log(`  api ready  : ${settings.api}/health/ready`);

  const tokens = {
    admin: await getToken('admin.user'),
    approver: await getToken('approver.user'),
    finance: await getToken('finance.user'),
    auditor: await getToken('auditor.user'),
  };

  log('  tokens acquired for admin.user, approver.user, finance.user, auditor.user');
  log('');

  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });

  const session = new SwaggerSession(page, {
    api: settings.api,
    outDir: gifDir,
    log,
    gif: { targetWidth: 900, delayMs: 750, firstDelayMs: 1100, maxColors: 256 },
  });

  const startedAt = new Date().toISOString();

  try {
    // 1 ─ open the UI
    session.beginStep('01-open-swagger', 'Open the Swagger UI');
    log('[1/18] open Swagger UI');
    await session.open();
    await session.frame();
    await session.endStep();

    // 2 ─ authorize
    session.beginStep('02-authorize-admin', 'Authorize with an admin token');
    log('[2/18] authorize as admin.user');
    const dialog = await session.openAuthorizeModal(tokens.admin);
    await session.frame(dialog);
    await session.confirmAuthorize(dialog);
    await session.frame();
    await session.endStep();

    // 3 ─ worker 1
    session.beginStep('03-create-worker-ada', `Create worker ${substitute(scenario.workers[0].externalRef)}`);
    log('[3/18] create worker 1');
    const worker1 = await captureOperation(session, {
      operationId: 'Workers-CreateWorker',
      body: {
        externalRef: substitute(scenario.workers[0].externalRef),
        displayName: scenario.workers[0].displayName,
        email: scenario.workers[0].email,
      },
      expect: { method: 'POST', path: '/api/v1/workers', status: 201 },
    });
    state.workerIds = [worker1.json.id];
    await session.endStep();

    // 4 ─ worker 2
    session.beginStep('04-create-worker-grace', `Create worker ${substitute(scenario.workers[1].externalRef)}`);
    log('[4/18] create worker 2');
    const worker2 = await captureOperation(session, {
      operationId: 'Workers-CreateWorker',
      body: {
        externalRef: substitute(scenario.workers[1].externalRef),
        displayName: scenario.workers[1].displayName,
        email: scenario.workers[1].email,
      },
      expect: { method: 'POST', path: '/api/v1/workers', status: 201 },
    });
    state.workerIds.push(worker2.json.id);
    await session.endStep();

    // 5 ─ contract 1
    session.beginStep('05-create-contract-ada', 'Create an effective-dated contract (hourly earning + 10% deduction)');
    log('[5/18] contract for worker 1');
    await captureOperation(session, {
      operationId: 'Workers-CreateContract',
      parameters: { id: state.workerIds[0] },
      body: scenario.workers[0].contract,
      expect: { method: 'POST', path: `/api/v1/workers/${state.workerIds[0]}/contracts`, status: 201 },
    });
    await session.endStep();

    // 6 ─ contract 2
    session.beginStep('06-create-contract-grace', 'Create the second contract');
    log('[6/18] contract for worker 2');
    await captureOperation(session, {
      operationId: 'Workers-CreateContract',
      parameters: { id: state.workerIds[1] },
      body: scenario.workers[1].contract,
      expect: { method: 'POST', path: `/api/v1/workers/${state.workerIds[1]}/contracts`, status: 201 },
    });
    await session.endStep();

    // 7 ─ register the import
    session.beginStep('07-register-import', 'Register the timesheet import and get a scoped upload URL');
    log('[7/18] register timesheet import');
    const registration = await captureOperation(session, {
      operationId: 'Timesheet_imports-RegisterTimesheetImport',
      body: {
        periodStart: scenario.period.start,
        periodEnd: scenario.period.end,
        fileName: csvName,
        sha256: csvSha,
        sizeBytes: csvBytes.length,
      },
      expect: { method: 'POST', path: '/api/v1/timesheet-imports', status: 201 },
    });
    state.importId = registration.json.importId;
    state.uploadUri = registration.json.uploadUri;
    await session.endStep();

    // 8 ─ upload + asynchronous processing
    session.beginStep('08-import-processed', 'Upload the CSV and let the Functions host validate and import it');
    log('[8/18] upload CSV and wait for the import function');
    const uploadStatus = await putBlob(state.uploadUri, csvBytes);
    log(`  blob PUT -> ${uploadStatus}`);

    const processed = await waitFor(
      'timesheet import to complete',
      async () => {
        const result = await callApi('GET', `/api/v1/timesheet-imports/${state.importId}`, { token: tokens.admin });
        return result.json?.status === 'Completed' || result.json?.status === 'Failed'
          ? { done: true, value: result.json }
          : { done: false };
      },
      { timeoutMs: 180000, intervalMs: 3000 },
    );

    assertEqual(processed.status, 'Completed', 'import status');
    assertEqual(processed.validRows, scenario.timesheet.length, 'valid rows');
    log(`  import ${processed.status}: ${processed.validRows} valid / ${processed.invalidRows} invalid`);

    await captureOperation(session, {
      operationId: 'Timesheet_imports-GetTimesheetImport',
      parameters: { id: state.importId },
      expect: { method: 'GET', path: `/api/v1/timesheet-imports/${state.importId}`, status: 200 },
    });
    await session.endStep();

    // 9 ─ create the payroll run
    session.beginStep('09-create-payroll-run', 'Create a payroll run over the September period');
    log('[9/18] create payroll run');
    const payrollRun = await captureOperation(session, {
      operationId: 'Payroll-CreatePayrollRun',
      body: {
        periodStart: scenario.period.start,
        periodEnd: scenario.period.end,
        currency: scenario.currency,
        sourceImportIds: [state.importId],
        note: `Walkthrough run ${marker}`,
      },
      expect: { method: 'POST', path: '/api/v1/payroll-runs', status: 201 },
    });
    state.runId = payrollRun.json.id;
    await session.endStep();

    // 10 ─ calculate
    session.beginStep('10-calculate-run', 'Calculate a deterministic gross-to-net snapshot');
    log('[10/18] calculate payroll run');
    const calculation = await captureOperation(session, {
      operationId: 'Payroll-CalculatePayrollRun',
      parameters: { id: state.runId },
      expect: { method: 'POST', path: `/api/v1/payroll-runs/${state.runId}/calculate`, status: 200 },
    });

    assertEqual(calculation.json.totals.workerCount, scenario.expected.workerCount, 'worker count');
    assertEqual(calculation.json.totals.gross, scenario.expected.gross, 'gross total');
    assertEqual(calculation.json.totals.deduction, scenario.expected.deduction, 'deduction total');
    assertEqual(calculation.json.totals.net, scenario.expected.net, 'net total');
    log(`  totals: gross ${calculation.json.totals.gross}, deductions ${calculation.json.totals.deduction}, net ${calculation.json.totals.net}`);
    await session.endStep();

    // 11 ─ submit
    session.beginStep('11-submit-run', 'Submit the run for approval (maker)');
    log('[11/18] submit payroll run');
    await captureOperation(session, {
      operationId: 'Payroll-SubmitPayrollRun',
      parameters: { id: state.runId },
      expect: { method: 'POST', path: `/api/v1/payroll-runs/${state.runId}/submit`, status: 200 },
    });
    await session.endStep();

    // 12 ─ approve as a different user
    session.beginStep('12-approve-run', 'Approve as approver.user — the creator cannot approve their own run');
    log('[12/18] approve payroll run as approver.user');
    const decision = await captureOperation(session, {
      operationId: 'Payroll-DecidePayrollRun',
      token: tokens.approver,
      parameters: { id: state.runId },
      body: { approve: true, reason: null },
      expect: { method: 'POST', path: `/api/v1/payroll-runs/${state.runId}/decisions`, status: 200 },
    });
    assertEqual(decision.json.status, 'Approved', 'run status after approval');
    assertEqual(decision.json.decidedBy, 'approver.user', 'decided by');
    await session.endStep();

    // 13 ─ read the run
    session.beginStep('13-read-run', 'Read the run: totals, calculation version and allowed actions');
    log('[13/18] read payroll run');
    await captureOperation(session, {
      operationId: 'Payroll-GetPayrollRun',
      parameters: { id: state.runId },
      expect: { method: 'GET', path: `/api/v1/payroll-runs/${state.runId}`, status: 200 },
    });
    await session.endStep();

    // 14 ─ variance
    session.beginStep('14-variance', 'Variance report against the prior period');
    log('[14/18] variance report');
    await captureOperation(session, {
      operationId: 'Payroll-GetPayrollVariance',
      parameters: { id: state.runId },
      expect: { method: 'GET', path: `/api/v1/payroll-runs/${state.runId}/variance`, status: 200 },
    });
    await session.endStep();

    // 15 ─ payment batch (finance)
    session.beginStep('15-create-payment-batch', 'Finance submits the payment batch with an Idempotency-Key');
    log('[15/18] create payment batch as finance.user');
    const idempotencyKey = `walkthrough-${marker}`;
    const batch = await captureOperation(session, {
      operationId: 'Payments-CreatePaymentBatch',
      token: tokens.finance,
      parameters: { id: state.runId, 'Idempotency-Key': idempotencyKey },
      expect: { method: 'POST', path: `/api/v1/payroll-runs/${state.runId}/payment-batches`, status: 201 },
    });
    state.batchId = batch.json.id;
    log(`  batch ${state.batchId} status=${batch.json.status} total=${batch.json.totalAmount}`);
    await session.endStep();

    // 16 ─ outbox dispatch + signed webhook settlement
    session.beginStep('16-payment-settled', 'Outbox dispatch submits to the provider; a signed webhook settles it');
    log('[16/18] wait for outbox dispatch, then post a signed settlement webhook');

    const submitted = await waitFor(
      'payment batch submission',
      async () => {
        const result = await callApi('GET', `/api/v1/payment-batches/${state.batchId}`, { token: tokens.finance });
        return result.json?.status && result.json.status !== 'Created'
          ? { done: true, value: result.json }
          : { done: false };
      },
      { timeoutMs: 180000, intervalMs: 4000 },
    );

    log(`  batch submitted: providerReference=${submitted.providerReference}`);

    const webhook = await postWebhook({
      eventType: 'payment.settled',
      paymentReference: submitted.providerReference,
      amount: submitted.totalAmount,
      currency: submitted.currency,
      occurredAt: new Date().toISOString(),
    });

    assertEqual(webhook.status, 202, 'webhook status');
    log(`  webhook accepted (${webhook.status})`);

    const settled = await waitFor(
      'payment settlement',
      async () => {
        const result = await callApi('GET', `/api/v1/payment-batches/${state.batchId}`, { token: tokens.finance });
        return result.json?.status === 'Settled' ? { done: true, value: result.json } : { done: false };
      },
      { timeoutMs: 120000, intervalMs: 3000 },
    );

    log(`  batch settled: ${settled.status}`);

    await captureOperation(session, {
      operationId: 'Payments-GetPaymentBatch',
      parameters: { id: state.batchId },
      expect: { method: 'GET', path: `/api/v1/payment-batches/${state.batchId}`, status: 200 },
    });
    await session.endStep();

    // 17 ─ audit timeline
    session.beginStep('17-audit-timeline', 'Auditor reads the immutable audit timeline');
    log('[17/18] search audit events as auditor.user');
    await captureOperation(session, {
      operationId: 'Audit-SearchAuditEvents',
      token: tokens.auditor,
      parameters: { limit: '20' },
      expect: { method: 'GET', path: '/api/v1/audit-events', status: 200 },
    });
    await session.endStep();

    // 18 ─ reconciliation
    session.beginStep('18-reconciliation', 'Finance reviews reconciliation exceptions');
    log('[18/18] list reconciliation cases as finance.user');
    await captureOperation(session, {
      operationId: 'Reconciliation-GetReconciliationCases',
      token: tokens.finance,
      parameters: { limit: '20' },
      expect: { method: 'GET', path: '/api/v1/reconciliation-cases', status: 200 },
    });
    await session.endStep();
  } catch (error) {
    log(`\nFAILED: ${error.message}`);
    fs.writeFileSync(path.join(artifactDir, 'failure.png'), await page.screenshot({ clip: CLIP }));
    fs.writeFileSync(path.join(artifactDir, 'failure.txt'), String(error.stack ?? error));
    await browser.close();
    throw error;
  }

  await browser.close();

  const manifest = {
    generatedAt: startedAt,
    finishedAt: new Date().toISOString(),
    runMarker: marker,
    testData: {
      csv: csvName,
      sha256: csvSha,
      workers: scenario.workers.map((worker) => substitute(worker.externalRef)),
      period: scenario.period,
      expected: scenario.expected,
      importId: state.importId,
      payrollRunId: state.runId,
      paymentBatchId: state.batchId,
    },
    steps: session.results,
  };

  fs.writeFileSync(path.join(artifactDir, 'manifest.json'), JSON.stringify(manifest, null, 2));

  log('');
  log(`Captured ${session.results.length} GIFs into docs/walkthrough/gifs/`);
  const totalKb = session.results.reduce((sum, step) => sum + step.bytes, 0) / 1024;
  log(`Total GIF size: ${totalKb.toFixed(0)} KB`);

  const failed = session.results.filter((step) => !step.frames);
  assert(failed.length === 0, `${failed.length} steps produced no frames`);
  assertEqual(session.results.length, 18, 'captured steps');
}

await main();
