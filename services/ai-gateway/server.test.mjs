import test from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { createGateway } from './server.mjs';

const token = 'test-gateway-token-24-characters-minimum';
const env = { HORIZON_GATEWAY_TOKEN: token, DEEPSEEK_API_KEY: 'test-deepseek-private-key',
  AZURE_OPENAI_API_KEY: 'test-azure-private-key', AZURE_OPENAI_ENDPOINT: 'https://example.openai.azure.com', AZURE_OPENAI_DEPLOYMENT: 'interview coach' };
const personal = { futureSelfLine: '你已经迈出一步。', quest: '打开一份材料并读两分钟', patternExplanation: '近期证据仍然有限。' };
const body = provider => ({ provider, context: { goal: '面试', recentPattern: '', evidence: ['D2 · 学习'] } });
function upstream(value = personal, finish = 'stop') {
  return Response.json({ choices: [{ finish_reason: finish, message: { content: JSON.stringify(value) } }] });
}
async function fixture(t, options = {}) {
  const calls = [];
  const server = createGateway({ env, fetchImpl: async (url, request) => { calls.push({ url, request }); return upstream(); }, ...options });
  server.listen(0, '127.0.0.1'); await once(server, 'listening');
  t.after(() => new Promise(resolve => { server.close(resolve); server.closeAllConnections(); }));
  const url = `http://127.0.0.1:${server.address().port}`;
  const post = (value, authorization = `Bearer ${token}`) => fetch(url + '/v1/personalize', {
    method: 'POST', headers: { Authorization: authorization, 'Content-Type': 'application/json' }, body: typeof value === 'string' ? value : JSON.stringify(value) });
  return { server, url, post, calls };
}
test('DeepSeek uses server credentials and returns only content fields', async t => {
  const f = await fixture(t);
  const response = await f.post(body('deepseek'));
  assert.equal(response.status, 200); assert.deepEqual(await response.json(), personal);
  assert.equal(f.calls[0].url, 'https://api.deepseek.com/chat/completions');
  assert.equal(f.calls[0].request.headers.Authorization, 'Bearer test-deepseek-private-key');
  const request = JSON.parse(f.calls[0].request.body);
  assert.equal(request.model, 'deepseek-chat'); assert.equal(request.response_format.type, 'json_object');
  assert.equal(f.calls[0].request.redirect, 'error');
  assert.equal(response.headers.get('cache-control'), 'no-store');
});
test('Azure uses deployment and API version instead of a model name', async t => {
  const f = await fixture(t); const response = await f.post(body('azure'));
  assert.equal(response.status, 200);
  assert.equal(f.calls[0].url, 'https://example.openai.azure.com/openai/deployments/interview%20coach/chat/completions?api-version=2024-10-21');
  assert.equal(f.calls[0].request.headers['api-key'], 'test-azure-private-key');
  assert.equal(JSON.parse(f.calls[0].request.body).max_completion_tokens, 1800);
});
test('authentication and unconfigured provider requests cannot call upstream', async t => {
  const f = await fixture(t, { env: { HORIZON_GATEWAY_TOKEN: token } });
  assert.equal((await f.post(body('deepseek'), 'Bearer invalid')).status, 401);
  assert.equal((await f.post(body('deepseek'))).status, 503); assert.equal(f.calls.length, 0);
});
test('request schema rejects arbitrary URLs, oversized evidence, unknown providers and malformed JSON', async t => {
  const f = await fixture(t);
  for (const value of [{ ...body('deepseek'), endpoint: 'http://localhost' }, body('unknown'),
    { provider: 'deepseek', context: { ...body('deepseek').context, evidence: Array(7).fill('x') } }, '{broken'])
    assert.equal((await f.post(value)).status, 400);
  assert.equal((await f.post('x'.repeat(17000))).status, 413); assert.equal(f.calls.length, 0);
});
test('provider error bodies are never exported and rate limits remain distinguishable', async t => {
  const f = await fixture(t, { fetchImpl: async () => new Response('private diagnostic test-deepseek-private-key', { status: 429 }) });
  const response = await f.post(body('deepseek')); assert.equal(response.status, 429);
  assert.deepEqual(await response.json(), { error: 'provider_rate_limited' });
});
test('empty, incomplete and oversized provider content is rejected', async t => {
  for (const value of [{}, { ...personal, quest: 'x'.repeat(141) }]) {
    const f = await fixture(t, { fetchImpl: async () => upstream(value) });
    assert.equal((await f.post(body('deepseek'))).status, 502);
  }
  const f = await fixture(t, { fetchImpl: async () => upstream(personal, 'length') });
  assert.equal((await f.post(body('deepseek'))).status, 502);
});
test('invented game-rule fields never enter the client response', async t => {
  const f = await fixture(t, { fetchImpl: async () => upstream({ ...personal, energy: 999, victory: true }) });
  assert.deepEqual(await (await f.post(body('deepseek'))).json(), personal);
});
test('provider timeouts abort upstream and return a safe error', async t => {
  let aborted = false;
  const f = await fixture(t, { timeoutMs: 30, fetchImpl: async (_, request) => new Promise((resolve, reject) => {
    request.signal.addEventListener('abort', () => { aborted = true; reject(new Error('private timeout')); }, { once: true });
  }) });
  const response = await f.post(body('azure'));
  assert.equal(response.status, 504); assert.equal(aborted, true); assert.deepEqual(await response.json(), { error: 'provider_timeout' });
});
test('private gateway throttles requests without exposing token or personal content', async t => {
  const f = await fixture(t, { requestsPerMinute: 1 });
  assert.equal((await f.post(body('deepseek'))).status, 200);
  assert.equal((await f.post(body('deepseek'))).status, 429);
  assert.equal(f.calls.length, 1);
  assert.deepEqual(await (await fetch(f.url + '/healthz')).json(), { status: 'ok' });
});
test('gateway refuses placeholder or absent authentication configuration', () => {
  assert.throws(() => createGateway({ env: {} }), /HORIZON_GATEWAY_TOKEN/);
  assert.throws(() => createGateway({ env: { HORIZON_GATEWAY_TOKEN: 'replace-with-a-long-random-private-access-token' } }), /HORIZON_GATEWAY_TOKEN/);
});
