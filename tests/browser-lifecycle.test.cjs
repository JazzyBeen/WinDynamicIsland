const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const root = path.join(__dirname, '../extensions/browser');
function event() { return { handlers: [], addListener(fn) { this.handlers.push(fn); } }; }
async function boot() {
  const chrome = { runtime: { getManifest: () => ({ version: '2.2.0' }), onStartup: event(), onInstalled: event(), onMessage: event() }, alarms: { create: async () => {}, onAlarm: event() }, tabs: { onRemoved: event(), sendMessage() {} } };
  const requests = [];
  const context = vm.createContext({ chrome, navigator: { userAgent: 'YaBrowser' }, importScripts() {}, ISLAND_ENDPOINT: 'http://127.0.0.1:48573', ISLAND_TOKEN: 'synthetic-test-token', Map, Set, Date, AbortSignal, setInterval() {}, fetch: async (url, options) => { requests.push({ url, options }); return { ok: true, json: async () => ({ commands: [] }) }; } });
  vm.runInContext(fs.readFileSync(path.join(root, 'background-http.js'), 'utf8'), context);
  await new Promise(resolve => setImmediate(resolve));
  return { chrome, requests };
}
(async () => {
  const first = await boot();
  assert.ok(first.requests.some(r => r.url.endsWith('/commands/Yandex')));
  await first.chrome.runtime.onStartup.handlers[0]();
  assert.ok(first.requests.every(r => r.options.headers.Authorization === 'Bearer synthetic-test-token'));
  const restarted = await boot();
  await restarted.chrome.alarms.onAlarm.handlers[0]({ name: 'island-reconnect' });
  await new Promise(resolve => setImmediate(resolve));
  assert.ok(restarted.requests.length >= 2);
  let response;
  restarted.chrome.runtime.onMessage.handlers[0]({ type: 'bridge-diagnostic' }, {}, value => response = value);
  assert.equal(response.version, '2.2.0');
  assert.equal(response.connected, true);
  console.log('PASS: fresh worker, browser startup, wake alarm and authenticated reconnect');
})().catch(error => { console.error(error); process.exitCode = 1; });
