import path from 'node:path';
import { encodeGif } from './gif.mjs';

export const VIEWPORT = { width: 1280, height: 800 };

/** Fixed capture window: keeps every frame in a step the same size so the GIF encoder accepts them. */
export const CLIP = { x: 0, y: 0, width: 1280, height: 690 };

/**
 * Drives Swagger UI and captures an animated GIF per step.
 *
 * Swagger UI renders each operation with a stable id of the form
 * `operations-<Tag>-<OperationId>`, taken from the OpenAPI document, and renders request
 * parameters as inputs whose `placeholder` is the parameter name.
 */
export class SwaggerSession {
  constructor(page, { api, outDir, gif = {}, log = console.log } = {}) {
    this.page = page;
    this.api = api;
    this.outDir = outDir;
    this.gifOptions = gif;
    this.log = log;

    this.responses = [];
    this.failures = [];
    this.step = null;
    this.results = [];

    page.on('response', (response) => {
      void this.record(response);
    });

    page.on('requestfailed', (request) => {
      this.failures.push({
        method: request.method(),
        path: (() => {
          try {
            return new URL(request.url()).pathname;
          } catch {
            return request.url();
          }
        })(),
        error: request.failure()?.errorText ?? 'unknown',
      });
    });
  }

  async record(response) {
    const url = response.url();

    if (!url.includes('/api/v1')) {
      return;
    }

    // Register the entry immediately: the status is known synchronously and callers poll for it,
    // while the body still has to be read asynchronously.
    const entry = {
      path: new URL(url).pathname,
      method: response.request().method(),
      status: response.status(),
      json: null,
      text: null,
    };

    this.responses.push(entry);

    try {
      entry.json = await response.json();
    } catch {
      try {
        entry.text = await response.text();
      } catch {
        /* response body unavailable */
      }
    }
  }

  /** Waits until the intercepted response body has been read. */
  async waitForBody(entry, timeoutMs = 15000) {
    const deadline = Date.now() + timeoutMs;

    while (Date.now() < deadline && entry.json === null && entry.text === null) {
      await this.page.waitForTimeout(100);
    }

    return entry;
  }

  lastResponse(method, pathPrefix) {
    return [...this.responses]
      .reverse()
      .find((entry) => entry.method === method && entry.path.startsWith(pathPrefix));
  }

  clearResponses() {
    this.responses = [];
  }

  async open() {
    await this.page.goto(`${this.api}/swagger`, { waitUntil: 'networkidle' });
    await this.page.locator('.opblock').first().waitFor({ state: 'visible', timeout: 30000 });
    await this.page.evaluate(() => window.scrollTo(0, 0));
    await this.page.waitForTimeout(300);
  }

  beginStep(id, title) {
    this.step = { id, title, frames: [] };
    this.clearResponses();
    this.failures = [];
  }

  /** Transport-level failures seen in the current step, useful when no response is intercepted. */
  describeFailures() {
    return this.failures.length
      ? this.failures.map((failure) => `${failure.method} ${failure.path} -> ${failure.error}`).join('; ')
      : 'no transport-level failures recorded';
  }

  async scrollTo(locator, padding = 12) {
    await locator
      .first()
      .evaluate((element, pad) => {
        const topbar = document.querySelector('.topbar');
        const offset = topbar ? topbar.getBoundingClientRect().height : 0;
        const y = element.getBoundingClientRect().top + window.scrollY - offset - pad;
        window.scrollTo({ top: Math.max(0, y), behavior: 'instant' });
      }, padding)
      .catch(() => {});
    await this.page.waitForTimeout(180);
  }

  /** Captures one frame. `scrollTo` moves the viewer before the shot without changing the clip. */
  async frame(scrollTo = null) {
    if (scrollTo) {
      await this.scrollTo(scrollTo);
    }

    const buffer = await this.page.screenshot({ clip: CLIP });
    this.step.frames.push(buffer);
  }

  async endStep() {
    const outPath = path.join(this.outDir, `${this.step.id}.gif`);
    const stat = encodeGif(this.step.frames, outPath, this.gifOptions);

    this.results.push({
      id: this.step.id,
      title: this.step.title,
      file: path.basename(outPath),
      ...stat,
    });

    this.log(
      `  -> ${path.basename(outPath)}  ${stat.frames} frames, ${(stat.bytes / 1024).toFixed(0)} KB, ${stat.width}x${stat.height}`,
    );

    this.step = null;
  }

  operation(operationId) {
    return this.page.locator(`[id="operations-${operationId}"]`).first();
  }

  async expandOperation(operationId) {
    const block = this.operation(operationId);
    await block.waitFor({ state: 'attached', timeout: 20000 });
    await this.scrollTo(block);

    if ((await block.locator('.opblock-body').count()) === 0) {
      await block.locator('.opblock-summary').click();
    }

    await block.locator('.opblock-body').waitFor({ state: 'visible', timeout: 15000 });

    return block;
  }

  /**
   * Puts an operation into an editable state. Swagger renders "Try it out" the first time and
   * "Cancel"/"Reset" afterwards, so a repeated operation must not click "Try it out" again.
   */
  async ensureEditable(block) {
    const toggle = block.locator('button.try-out__btn').first();
    await toggle.waitFor({ state: 'visible', timeout: 15000 });

    const label = ((await toggle.textContent()) ?? '').trim().toLowerCase();

    if (label.startsWith('try')) {
      await toggle.click();
      await this.page.waitForTimeout(300);
      return;
    }

    const reset = block.locator('button.try-out__btn.reset').first();

    if (await reset.count()) {
      await reset.click();
      await this.page.waitForTimeout(300);
    }
  }

  async fillParameter(block, name, value) {
    const input = block.locator(`input[placeholder="${name}"]`).first();
    await input.waitFor({ state: 'visible', timeout: 10000 });
    await input.fill(String(value));
  }

  async fillBody(block, value) {
    const textarea = block.locator('textarea.body-param__text').first();
    await textarea.waitFor({ state: 'visible', timeout: 10000 });
    await textarea.fill(typeof value === 'string' ? value : JSON.stringify(value, null, 2));
  }

  /**
   * Executes and waits for the intercepted response, so slow commands (calculation, payment batch) are
   * not sampled before the server has answered.
   */
  async execute(block, expect) {
    await block.locator('button.execute').first().click();

    const deadline = Date.now() + 60000;

    while (Date.now() < deadline) {
      const seen = expect ? this.lastResponse(expect.method, expect.path) : null;

      if (seen) {
        await this.waitForBody(seen);
        break;
      }

      await this.page.waitForTimeout(200);
    }

    // Let the response body render in the DOM before the frame is captured.
    await this.page.waitForTimeout(600);

    return block.locator('.live-responses-table, .responses-inner').first();
  }

  /**
   * Clears Swagger's persisted authorization and reloads. Swagger hides the token input behind a Logout
   * button once a scheme is authorized, so switching users is done from a clean state rather than by
   * fighting the modal's authorized layout.
   */
  async resetAuthorization() {
    await this.page.evaluate(() => {
      try {
        window.localStorage.removeItem('authorized');
      } catch {
        /* storage unavailable */
      }
    });

    await this.page.reload({ waitUntil: 'domcontentloaded' });
    await this.page.locator('.opblock').first().waitFor({ state: 'visible', timeout: 30000 });
    await this.page.waitForTimeout(400);
  }

  async openAuthorizeModal(token) {
    await this.resetAuthorization();

    await this.page.locator('button.authorize').first().click();

    const dialog = this.page.locator('.dialog-ux');
    await dialog.waitFor({ state: 'visible', timeout: 15000 });

    const input = dialog.locator('input[type="text"]').first();
    await input.waitFor({ state: 'visible', timeout: 15000 });
    await input.fill(token);

    return dialog;
  }

  async confirmAuthorize(dialog) {
    await dialog.locator('button.btn.modal-btn.auth.authorize').click();
    await this.page.waitForTimeout(400);
    await dialog.locator('button.btn.modal-btn.auth.btn-done').click();
    await dialog.waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
    await this.page.waitForTimeout(300);
  }
}
