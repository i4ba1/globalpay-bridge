import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { callApi, getToken, putBlob, sha256Hex, settings, waitFor, waitForApiReady } from './lib/http.mjs';

/**
 * Seeds the tenant with test data through the public API so the Swagger UI can be explored by hand.
 *
 * Creates two workers with effective-dated contracts, registers and uploads a September timesheet, and
 * reports the identifiers it created. Worker references carry a run marker so repeated runs do not
 * collide with the unique worker-reference constraint.
 */
const here = path.dirname(fileURLToPath(import.meta.url));
const artifactDir = path.resolve(here, '../../docs/walkthrough/artifacts');

const scenario = JSON.parse(fs.readFileSync(path.join(here, 'testdata/scenario.json'), 'utf8'));

const marker = process.env.GPAY_RUN ?? String(Date.now()).slice(-4);
const substitute = (template) => template.replaceAll('{{RUN}}', marker);

function assertOk(response, label) {
  if (response.status >= 300) {
    throw new Error(`${label} failed: ${response.status} ${JSON.stringify(response.json)}`);
  }

  return response.json;
}

function csvFor(refs) {
  const lines = ['workerRef,workDate,units,componentCode'];

  for (const row of scenario.timesheet) {
    lines.push([refs.get(row.worker), row.workDate, row.units, row.componentCode ?? ''].join(','));
  }

  return Buffer.from(`${lines.join('\n')}\n`, 'utf8');
}

await waitForApiReady();

const admin = await getToken('admin.user');
const refs = new Map(scenario.workers.map((worker) => [worker.key, substitute(worker.externalRef)]));

console.log(`seeding tenant with run marker ${marker}`);
console.log(`  period ${scenario.period.start} .. ${scenario.period.end}`);

const workerIds = new Map();

for (const worker of scenario.workers) {
  const created = assertOk(
    await callApi('POST', '/api/v1/workers', {
      token: admin,
      body: {
        externalRef: substitute(worker.externalRef),
        displayName: worker.displayName,
        email: worker.email,
      },
    }),
    `create worker ${worker.externalRef}`,
  );

  workerIds.set(worker.key, created.id);

  assertOk(
    await callApi('POST', `/api/v1/workers/${created.id}/contracts`, { token: admin, body: worker.contract }),
    `create contract for ${worker.externalRef}`,
  );

  console.log(`  worker ${created.externalRef} (${created.id}) with a contract`);
}

const csv = csvFor(refs);
const csvName = `timesheets-2026-09-${marker}.csv`;

const registration = assertOk(
  await callApi('POST', '/api/v1/timesheet-imports', {
    token: admin,
    body: {
      periodStart: scenario.period.start,
      periodEnd: scenario.period.end,
      fileName: csvName,
      sha256: sha256Hex(csv),
      sizeBytes: csv.length,
    },
  }),
  'register timesheet import',
);

await putBlob(registration.uploadUri, csv);

const imported = await waitFor(
  'timesheet import to complete',
  async () => {
    const result = await callApi('GET', `/api/v1/timesheet-imports/${registration.importId}`, { token: admin });

    return result.json?.status === 'Completed' || result.json?.status === 'Failed'
      ? { done: true, value: result.json }
      : { done: false };
  },
  { timeoutMs: 180000, intervalMs: 3000 },
);

fs.mkdirSync(artifactDir, { recursive: true });
fs.writeFileSync(path.join(artifactDir, csvName), csv);

console.log(`  import ${imported.status}: ${imported.validRows} valid / ${imported.invalidRows} invalid`);
console.log('');
console.log('seed complete. Use these values in the Swagger UI:');
console.log(`  worker ids      : ${[...workerIds.values()].join(', ')}`);
console.log(`  sourceImportIds : ${registration.importId}`);
console.log(`  period          : ${scenario.period.start} .. ${scenario.period.end}`);
console.log(`  expected totals : gross ${scenario.expected.gross}, deductions ${scenario.expected.deduction}, net ${scenario.expected.net} for ${scenario.expected.workerCount} workers`);
console.log(`  api             : ${settings.api}/swagger`);
