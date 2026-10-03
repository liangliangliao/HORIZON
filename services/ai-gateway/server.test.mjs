import test from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { createGateway } from './server.mjs';

const token = 'test-gateway-token-24-characters-minimum';
const env = { HORIZON_GATEWAY_TOKEN: token, DEEPSEEK_API_KEY: 'test-deepseek-private-key',
  AZURE_OPENAI_API_KEY: 'test-azure-private-key', AZURE_OPENAI_ENDPOINT: 'https://example.openai.azure.com', AZURE_OPENAI_DEPLOYMENT: 'interview coach' };
const personal = { futureSelfLine: '你已经迈出一步。', quest: '打开一份材料并读两分钟', patternExplanation: '近期证据仍然有限。' };
test('pasted Responses operation URL takes priority over stale Foundry mode and discovers model', async t => {
  const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: 'https://modleapikey-resource.services.ai.azure.com/openai/v1/responses',
    AZURE_ACCESS_MODE: 'foundry', AZURE_OPENAI_DEPLOYMENT: '' }, i => i === 0 ? Response.json({ data: [{ id: 'coach' }] }) :
    Response.json({ status: 'completed', output: [{ type: 'reasoning' }, { type: 'message', status: 'completed', content: [{ type: 'output_text', text: JSON.stringify(personal) }] }] }));
  const response = await f.post(body('azure'));
  assert.equal(response.status, 200); assert.deepEqual(await response.json(), personal);
  assert.equal(f.calls[1].url, 'https://modleapikey-resource.services.ai.azure.com/openai/v1/responses');
  const payload = JSON.parse(f.calls[1].request.body);
  assert.equal(payload.model, 'coach'); assert.equal(payload.store, false); assert.equal(payload.text.format.type, 'json_object');
  assert.equal(payload.messages, undefined); assert.equal(payload.max_output_tokens, 1800);
});
test('Responses incomplete or refused output is rejected', async t => {
  for (const response of [{ status: 'incomplete', output: [] }, { status: 'completed', output: [{ type: 'message', status: 'completed', content: [{ type: 'refusal', refusal: 'no' }] }] }]) {
    const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: 'https://example.services.ai.azure.com/openai/v1/responses' }, () => Response.json(response));
    assert.equal((await f.post(body('azure'))).status, 502);
  }
});
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
async function sequence(t, config, response) {
  const calls = [];
  const f = await fixture(t, { env: { ...env, ...config }, fetchImpl: async (url, request) => {
    calls.push({ url, request }); return response(calls.length - 1, url, request);
  } });
  return { ...f, calls };
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
test('Foundry project endpoint is normalized to the same resource v1 endpoint', async t => {
  const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: 'https://example.services.ai.azure.com/api/projects/interview',
    AZURE_OPENAI_DEPLOYMENT: 'coach', AZURE_OPENAI_API_VERSION: '' }, () => upstream());
  assert.equal((await f.post(body('azure'))).status, 200);
  assert.equal(f.calls[0].url, 'https://example.services.ai.azure.com/openai/v1/chat/completions');
  assert.equal(JSON.parse(f.calls[0].request.body).model, 'coach');
  assert.equal(f.calls[0].request.headers['api-key'], env.AZURE_OPENAI_API_KEY);
});
test('explicit Foundry mode uses Models API and its own default version', async t => {
  const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: 'https://example.services.ai.azure.com/api/projects/interview',
    AZURE_OPENAI_DEPLOYMENT: 'coach', AZURE_ACCESS_MODE: 'foundry', AZURE_OPENAI_API_VERSION: '' }, () => upstream());
  assert.equal((await f.post(body('azure'))).status, 200);
  assert.equal(f.calls[0].url, 'https://example.services.ai.azure.com/models/chat/completions?api-version=2024-05-01-preview');
  assert.equal(JSON.parse(f.calls[0].request.body).max_tokens, 900);
});
test('serverless inference endpoints retain their root path and SDK-compatible key headers', async t => {
  const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: 'https://coach.models.ai.azure.com', AZURE_OPENAI_DEPLOYMENT: 'coach' }, () => upstream());
  assert.equal((await f.post(body('azure'))).status, 200);
  assert.equal(f.calls[0].url, 'https://coach.models.ai.azure.com/chat/completions?api-version=2024-05-01-preview');
  assert.equal(f.calls[0].request.headers.Authorization, `Bearer ${env.AZURE_OPENAI_API_KEY}`);
  assert.equal(f.calls[0].request.headers['api-key'], env.AZURE_OPENAI_API_KEY);
});
test('explicit v1 mode accepts an openai/v1 base without repeating its path or dated version', async t => {
  const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: 'https://example.openai.azure.com/openai/v1/',
    AZURE_ACCESS_MODE: 'v1', AZURE_OPENAI_API_VERSION: '2024-10-21' }, () => upstream());
  assert.equal((await f.post(body('azure'))).status, 200);
  assert.equal(f.calls[0].url, 'https://example.openai.azure.com/openai/v1/chat/completions');
  assert.equal(JSON.parse(f.calls[0].request.body).model, 'interview coach');
});
test('missing traditional deployment path can fall back to v1 on the same origin', async t => {
  const f = await sequence(t, {}, i => i === 0 ? new Response('private diagnostic', { status: 404 }) : upstream());
  assert.equal((await f.post(body('azure'))).status, 200); assert.equal(f.calls.length, 2);
  assert.match(f.calls[0].url, /openai\/deployments\//);
  assert.equal(f.calls[1].url, 'https://example.openai.azure.com/openai/v1/chat/completions');
  assert.equal(f.calls[0].request.signal, f.calls[1].request.signal);
});
test('Foundry project retries v1 then Models when only the preferred path is missing', async t => {
  const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: 'https://example.services.ai.azure.com/api/projects/interview' },
    i => i === 0 ? new Response('', { status: 404 }) : upstream());
  assert.equal((await f.post(body('azure'))).status, 200); assert.equal(f.calls.length, 2);
  assert.match(f.calls[1].url, /\/models\/chat\/completions\?api-version=2024-05-01-preview$/);
});
test('comma-separated deployments try the next name only after missing paths', async t => {
  const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: 'https://example.services.ai.azure.com', AZURE_OPENAI_DEPLOYMENT: 'missing，coach,coach' },
    i => i < 3 ? new Response('', { status: 404 }) : upstream());
  assert.equal((await f.post(body('azure'))).status, 200); assert.equal(f.calls.length, 4);
  assert.equal(JSON.parse(f.calls[0].request.body).model, 'missing'); assert.equal(JSON.parse(f.calls[3].request.body).model, 'coach');
});
test('only explicit missing-deployment errors allow a 400 retry', async t => {
  const f = await sequence(t, {}, i => i === 0 ? Response.json({ error: { code: 'DeploymentNotFound' } }, { status: 400 }) : upstream());
  assert.equal((await f.post(body('azure'))).status, 200); assert.equal(f.calls.length, 2);
});
test('authentication, rate limits, other 400 errors and server failures never retry routes or models', async t => {
  for (const status of [401, 403, 429, 400, 500, 503, 504]) {
    const f = await sequence(t, { AZURE_OPENAI_DEPLOYMENT: 'one,two' }, () => new Response('private diagnostic', { status }));
    assert.equal((await f.post(body('azure'))).status, status === 429 ? 429 : status === 504 ? 504 : 502);
    assert.equal(f.calls.length, 1);
  }
});
test('empty deployment lists use GET v1 models and exactly the returned id', async t => {
  const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: 'https://example.services.ai.azure.com/api/projects/interview', AZURE_OPENAI_DEPLOYMENT: '' },
    i => i === 0 ? Response.json({ data: [{ id: 'discovered-coach' }] }) : upstream());
  assert.equal((await f.post(body('azure'))).status, 200); assert.equal(f.calls.length, 2);
  assert.equal(f.calls[0].url, 'https://example.services.ai.azure.com/openai/v1/models');
  assert.equal(f.calls[0].request.method, 'GET'); assert.equal(f.calls[0].request.body, undefined);
  assert.equal(JSON.parse(f.calls[1].request.body).model, 'discovered-coach');
});
test('unavailable model listing requests an explicit deployment without guessing', async t => {
  for (const result of [() => new Response('', { status: 404 }), () => Response.json({ data: [] })]) {
    const f = await sequence(t, { AZURE_OPENAI_DEPLOYMENT: '' }, result);
    const response = await f.post(body('azure'));
    assert.equal(response.status, 503); assert.deepEqual(await response.json(), { error: 'provider_deployment_required' }); assert.equal(f.calls.length, 1);
  }
});
test('unsafe and unrecognized Azure endpoint formats fail before any upstream call', async t => {
  for (const endpoint of ['http://example.services.ai.azure.com/api/projects/demo', 'https://user:key@example.openai.azure.com',
    'https://example.openai.azure.com?api-key=hidden', 'https://example.openai.azure.com/#hidden', 'https://example.services.ai.azure.com/api/projects/demo/unknown']) {
    const f = await sequence(t, { AZURE_OPENAI_ENDPOINT: endpoint }, () => upstream());
    assert.equal((await f.post(body('azure'))).status, 503); assert.equal(f.calls.length, 0);
  }
});
test('Azure deployment count and names are bounded', async t => {
  for (const deployment of ['a,b,c,d,e,f,g', 'x'.repeat(101)]) {
    const f = await sequence(t, { AZURE_OPENAI_DEPLOYMENT: deployment }, () => upstream());
    assert.equal((await f.post(body('azure'))).status, 503); assert.equal(f.calls.length, 0);
  }
});
