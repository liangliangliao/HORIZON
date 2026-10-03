import test from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { mkdtempSync, rmSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { createSocialServer } from './server.mjs';
async function fixture(t, options = {}) {
  const server = createSocialServer(options); server.listen(0, '127.0.0.1'); await once(server, 'listening');
  t.after(() => new Promise(resolve => { server.close(resolve); server.closeAllConnections(); }));
  const root = `http://127.0.0.1:${server.address().port}`;
  return { server, root, call: (path, value, token) => fetch(root + path, { method: value ? 'POST' : 'GET',
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: value ? JSON.stringify(value) : undefined }) };
}
async function pair(t, options) {
  const f = await fixture(t, options); const a = await (await f.call('/v1/rooms', { worldSeed: 17, name: '旅行者甲' })).json();
  const path = '/v1/rooms/' + a.room.code; const b = await (await f.call(path + '/join', { name: '旅行者乙' })).json();
  return { ...f, a, b, path };
}
const timeline = (id = 'walk', count = 12) => ({ worldSeed: 17, runNumber: 1, catalogVersion: 10, completed: count === 12,
  actions: Array.from({ length: count }, (_, i) => ({ day: i + 1, cardId: id, cardName: id === 'walk' ? '出去走走' : '练习一次' })), resources: [6, 5, 4, 5, 6, 7] });
test('two real HTTP clients share a scenario, publish distinct lives and receive a future message', async t => {
  const f = await pair(t); assert.equal(f.a.room.worldSeed, f.b.room.worldSeed); assert.notEqual(f.a.memberToken, f.b.memberToken);
  assert.equal((await f.call(f.path + '/timeline', { timeline: timeline() }, f.a.memberToken)).status, 200);
  assert.equal((await f.call(f.path + '/timeline', { timeline: timeline('study') }, f.b.memberToken)).status, 200);
  await f.call(f.path + '/messages', { day: 5, text: '失败以后，我先恢复，再重新行动。' }, f.a.memberToken);
  const room = await (await f.call(f.path, null, f.b.memberToken)).json();
  assert.equal(room.members.length, 2); assert.equal(room.members[0].timeline.actions[0].cardId, 'walk');
  assert.equal(room.members[1].timeline.actions[0].cardId, 'study'); assert.equal(room.messages[0].day, 5);
  assert.equal(room.messages[0].text, '失败以后，我先恢复，再重新行动。');
  assert.ok(!JSON.stringify(room).includes(f.a.memberToken)); assert.ok(!JSON.stringify(room).includes('tokenHash'));
});
test('membership proof is required and a third person cannot occupy the same two-person room', async t => {
  const f = await pair(t); assert.equal((await f.call(f.path)).status, 401); assert.equal((await f.call(f.path, null, 'wrong')).status, 401);
  assert.equal((await f.call(f.path + '/join', { name: '第三个人' })).status, 409);
});
test('private fields are projected out of published stories and messages', async t => {
  const f = await pair(t); const input = timeline(); input.aiKey = 'not-an-uploadable-field'; input.playerModel = { private: true }; input.actions[0].goal = 'private goal';
  await f.call(f.path + '/timeline', { timeline: input }, f.a.memberToken);
  const raw = await (await f.call(f.path, null, f.b.memberToken)).text(); assert.ok(!raw.includes('private')); assert.ok(!raw.includes('not-an-uploadable-field'));
});
test('room data survives a service restart without storing raw member tokens', async t => {
  const dir = mkdtempSync(join(tmpdir(), 'horizon-social-')); t.after(() => rmSync(dir, { recursive: true, force: true }));
  const file = join(dir, 'rooms.json'); const f = await pair(t, { file });
  await f.call(f.path + '/timeline', { timeline: timeline('study', 4) }, f.a.memberToken);
  const second = await fixture(t, { file }); const room = await (await second.call(f.path, null, f.b.memberToken)).json();
  assert.equal(room.members[0].timeline.actions.length, 4); assert.ok(!readFileSync(file, 'utf8').includes(f.a.memberToken));
});
test('different seeds, oversized stories and rollback cannot alter the comparison', async t => {
  const f = await pair(t);
  for (const value of [{ ...timeline(), worldSeed: 18 }, { ...timeline(), actions: [...timeline().actions, { day: 13, cardId: 'walk', cardName: '走走' }] },
    { ...timeline(), resources: [100, 0, 0, 0, 0, 0] }, { ...timeline(), completed: true, actions: [] }])
    assert.equal((await f.call(f.path + '/timeline', { timeline: value }, f.a.memberToken)).status, 400);
  await f.call(f.path + '/timeline', { timeline: timeline() }, f.a.memberToken);
  assert.equal((await f.call(f.path + '/timeline', { timeline: timeline('walk', 3) }, f.a.memberToken)).status, 400);
});
test('expired opportunities are removed and experience messages have a finite limit', async t => {
  let clock = 100000; const f = await pair(t, { now: () => clock });
  for (let i = 0; i < 8; i++) assert.equal((await f.call(f.path + '/messages', { day: 4, text: '一段经验' }, f.a.memberToken)).status, 200);
  assert.equal((await f.call(f.path + '/messages', { day: 4, text: '第九段' }, f.a.memberToken)).status, 429);
  clock += 8 * 86400000; assert.equal((await f.call(f.path, null, f.a.memberToken)).status, 404);
});
