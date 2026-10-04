import http from 'node:http';
import { randomBytes, createHash, timingSafeEqual } from 'node:crypto';
import { readFileSync, writeFileSync, renameSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { pathToFileURL } from 'node:url';

const digest = value => createHash('sha256').update(value).digest('hex');
const secret = () => randomBytes(24).toString('hex');
const identifier = value => typeof value === 'string' && /^[a-zA-Z0-9:_-]{1,100}$/.test(value);
const short = (value, max) => typeof value === 'string' && value.trim().length > 0 && value.length <= max && !/[\u0000-\u001f]/.test(value);
class Rejection extends Error { constructor(status, reason) { super(reason); this.status = status; } }
export function createSocialServer({ file, now = Date.now, maxRooms = 1000 } = {}) {
  let rooms = {};
  if (file) { try { rooms = JSON.parse(readFileSync(file, 'utf8')); } catch (e) { if (e.code !== 'ENOENT') throw e; } }
  const rates = new Map();
  function save() { if (!file) return; mkdirSync(dirname(file), { recursive: true }); writeFileSync(file + '.tmp', JSON.stringify(rooms), { mode: 0o600 }); renameSync(file + '.tmp', file); }
  function publicRoom(room) { return { code: room.code, worldSeed: room.worldSeed, catalogVersion: 10, days: 12, runNumber: 1,
    expiresAt: room.expiresAt, members: room.members.map(m => ({ id: m.id, name: m.name, timeline: m.timeline })), messages: room.messages }; }
  function member(room, req) {
    const token = (req.headers.authorization || '').replace(/^Bearer /, '');
    const expected = Buffer.from(digest(token), 'hex');
    const found = room.members.find(m => timingSafeEqual(expected, Buffer.from(m.tokenHash, 'hex')));
    if (!found) throw new Rejection(401, 'member_required'); return found;
  }
  function reply(res, status, value) { res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' }); res.end(JSON.stringify(value)); }
  const server = http.createServer(async (req, res) => {
    try {
      if (req.url === '/healthz' && req.method === 'GET') { reply(res, 200, { status: 'ok', baseline: '0.4.2' }); return; }
      const address = req.socket.remoteAddress || ''; const minute = Math.floor(now() / 60000), rate = rates.get(address);
      if (!rate || rate.minute !== minute) rates.set(address, { minute, count: 1 }); else if (++rate.count > 90) throw new Rejection(429, 'rate_limited');
      if (rates.size > 5000) for (const [key, value] of rates) if (value.minute !== minute) rates.delete(key);
      for (const [code, room] of Object.entries(rooms)) if (room.expiresAt < now()) delete rooms[code];
      let input = {};
      if (req.method === 'POST') {
        let size = 0; const chunks = [];
        for await (const chunk of req) { size += chunk.length; if (size > 32768) throw new Rejection(413, 'too_large'); chunks.push(chunk); }
        try { input = JSON.parse(Buffer.concat(chunks)); } catch { throw new Rejection(400, 'invalid_json'); }
        if (!input || Array.isArray(input)) throw new Rejection(400, 'invalid_request');
      }
      if (req.method === 'POST' && req.url === '/v1/rooms') {
        if (!Number.isInteger(input.worldSeed) || input.worldSeed < -2147483648 || input.worldSeed > 2147483647 || !short(input.name, 30)) throw new Rejection(400, 'invalid_scenario');
        if (Object.keys(rooms).length >= maxRooms) throw new Rejection(503, 'capacity');
        let code; do { code = randomBytes(5).toString('hex').toUpperCase(); } while (rooms[code]);
        const token = secret(), host = { id: randomBytes(8).toString('hex'), name: input.name.trim(), tokenHash: digest(token), timeline: null };
        const room = rooms[code] = { code, worldSeed: input.worldSeed, expiresAt: now() + 7 * 86400000, members: [host], messages: [] }; save();
        reply(res, 201, { room: publicRoom(room), memberId: host.id, memberToken: token }); return;
      }
      const route = /^\/v1\/rooms\/([A-F0-9]{10})(?:\/(join|timeline|messages))?$/.exec(req.url);
      if (!route) throw new Rejection(404, 'not_found');
      const room = rooms[route[1]]; if (!room) throw new Rejection(404, 'room_expired');
      if (route[2] === 'join' && req.method === 'POST') {
        if (!short(input.name, 30)) throw new Rejection(400, 'invalid_name');
        if (room.members.length >= 2) throw new Rejection(409, 'room_full');
        const token = secret(), joined = { id: randomBytes(8).toString('hex'), name: input.name.trim(), tokenHash: digest(token), timeline: null };
        room.members.push(joined); save(); reply(res, 200, { room: publicRoom(room), memberId: joined.id, memberToken: token }); return;
      }
      const actor = member(room, req);
      if (!route[2] && req.method === 'GET') { reply(res, 200, publicRoom(room)); return; }
      if (route[2] === 'timeline' && req.method === 'POST') {
        const t = input.timeline;
        if (!t || t.worldSeed !== room.worldSeed || t.runNumber !== 1 || t.catalogVersion !== 10 || !Array.isArray(t.actions) || t.actions.length > 12 ||
          t.actions.some((a, i) => !a || a.day !== i + 1 || !identifier(a.cardId) || !short(a.cardName, 100)) ||
          !Array.isArray(t.resources) || t.resources.length !== 6 || t.resources.some(v => !Number.isInteger(v) || v < 0 || v > 10) ||
          typeof t.completed !== 'boolean' || t.completed && t.actions.length !== 12 || actor.timeline && actor.timeline.actions.length > t.actions.length)
          throw new Rejection(400, 'invalid_timeline');
        // Project a public story, not the private behavioral model, imagination, goals, keys or save file.
        actor.timeline = { worldSeed: room.worldSeed, runNumber: 1, catalogVersion: 10, completed: t.completed,
          actions: t.actions.map(a => ({ day: a.day, cardId: a.cardId, cardName: a.cardName })), resources: t.resources };
        save(); reply(res, 200, publicRoom(room)); return;
      }
      if (route[2] === 'messages' && req.method === 'POST') {
        if (!short(input.text, 200) || !Number.isInteger(input.day) || input.day < 1 || input.day > 12) throw new Rejection(400, 'invalid_message');
        if (room.messages.filter(m => m.memberId === actor.id).length >= 8) throw new Rejection(429, 'message_limit');
        room.messages.push({ id: secret(), memberId: actor.id, text: input.text.trim(), day: input.day }); save(); reply(res, 200, publicRoom(room)); return;
      }
      throw new Rejection(404, 'not_found');
    } catch (e) { reply(res, e instanceof Rejection ? e.status : 500, { error: e instanceof Rejection ? e.message : 'unavailable' }); }
  });
  server.requestTimeout = 20000; server.headersTimeout = 10000; return server;
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const server = createSocialServer({ file: process.env.HORIZON_SOCIAL_DATA || './data/parallel-lives.json' });
  server.listen(Number(process.env.PORT || 8788), process.env.HOST || '127.0.0.1');
}
