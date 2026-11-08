'use strict';
const assert = require('assert');
const { RoomStore, publicRoom, cleanName } = require('../rooms');

let clock = 1000000;
const store = new RoomStore(() => clock);
let passed = 0;
function test(name, fn) { fn(); passed++; console.log('ok -', name); }

test('create issues a token and slot 0', () => {
  const r = store.create('abc1', 'Mona');
  assert(r.ok && r.slot === 0 && r.token.length === 24);
});

test('create cannot overwrite a live room (409)', () => {
  const r = store.create('ABC1', 'Intruder');
  assert(!r.ok && r.status === 409);
});

test('invalid codes are rejected', () => {
  assert(!store.create('a', 'x').ok);
  assert(!store.create('', 'x').ok);
});

let guest;
test('join assigns next slot and a different token', () => {
  guest = store.join('ABC1', 'Omar');
  assert(guest.ok && guest.slot === 1);
  assert(guest.token !== store.rooms.get('ABC1').tokens[0]);
});

test('public room never leaks tokens', () => {
  const json = JSON.stringify(publicRoom(store.rooms.get('ABC1')));
  assert(!json.includes(guest.token));
  assert(!json.includes('token'));
});

test('names are sanitized (no markup, max 16)', () => {
  assert.strictEqual(cleanName('<b>Evil</b>\u0007Name12345678901234', 'x'), 'bEvil/bName12345');
  assert.strictEqual(cleanName('   ', 'Fallback'), 'Fallback');
});

test('sync needs the right token for the slot', () => {
  const room = store.rooms.get('ABC1');
  assert(!store.sync('ABC1', { slot: 0, token: guest.token, x: 1, z: 1 }).ok);
  assert(!store.sync('ABC1', { slot: 1, token: 'nope', x: 1, z: 1 }).ok);
  assert(store.sync('ABC1', { slot: 1, token: guest.token, x: 1, z: 1 }).ok);
  assert(room.players[1].hasPos === true);
});

test('rev only moves on real changes and unchanged replies are flagged', () => {
  const t = guest.token;
  const a = store.sync('ABC1', { slot: 1, token: t, x: 2, z: 2, rotY: 10, isWalking: true, rev: 0 });
  const b = store.sync('ABC1', { slot: 1, token: t, x: 2, z: 2, rotY: 10, isWalking: true, rev: a.rev });
  assert(b.rev === a.rev && b.unchanged === true);
  const c = store.sync('ABC1', { slot: 1, token: t, x: 3, z: 2, rev: b.rev });
  assert(c.rev === b.rev + 1 && !c.unchanged);
});

test('sync rejects garbage numbers instead of storing them', () => {
  const r = store.sync('ABC1', { slot: 1, token: guest.token, x: 'NaN', z: Infinity });
  assert(r.ok);
  const p = store.rooms.get('ABC1').players[1];
  assert(p.x === 3 && Number.isFinite(p.z));
});

test('only host can start; guests get 403', () => {
  assert(store.start('ABC1', 1, guest.token).status === 403);
  const host = store.rooms.get('ABC1').tokens[0];
  assert(store.start('ABC1', 0, host).ok);
  assert(store.rooms.get('ABC1').isPlaying);
});

test('cannot join a running match', () => {
  assert(store.join('ABC1', 'Late').status === 400);
});

test('deliver counts once per seq (retry safe) and clamps points', () => {
  const t = guest.token;
  const first = store.deliver('ABC1', { slot: 1, token: t, seq: 1, earnedPoints: 999999, dishName: 'Burger' });
  const retry = store.deliver('ABC1', { slot: 1, token: t, seq: 1, earnedPoints: 100 });
  assert(first.ok && !first.duplicate && retry.duplicate);
  const p = store.rooms.get('ABC1').players[1];
  assert(p.dishes === 1 && p.score === 1000);
});

test('silent guest is swept, host keeps the room alive', () => {
  const room = store.rooms.get('ABC1');
  const host = room.tokens[0];
  clock += 25000;
  store.sync('ABC1', { slot: 0, token: host, x: 0, z: 0 }); // host heartbeat
  clock += 10000;
  store.sync('ABC1', { slot: 0, token: host, x: 0, z: 0 });
  assert.strictEqual(store.sweep(), 1);
  assert(room.players.length === 1 && room.players[0].slot === 0);
});

test('host leaving promotes the next player; empty room is deleted', () => {
  const g = store.join('ZZZ9', 'x');
  assert(!g.ok); // no such room
  const c = store.create('PRO1', 'Host');
  const j = store.join('PRO1', 'Guest');
  store.leave('PRO1', 0, c.token);
  const room = store.rooms.get('PRO1');
  assert(room.players[0].slot === 1 && room.players[0].isHost && room.host === 'Guest');
  store.leave('PRO1', 1, j.token);
  assert(!store.rooms.has('PRO1'));
});

test('stale rooms can have their code reclaimed', () => {
  const c = store.create('OLD1', 'A');
  clock += 60000;
  const again = store.create('OLD1', 'B');
  assert(again.ok && again.room.host === 'B');
});

test('leave with a bad token does nothing', () => {
  const c = store.create('SAFE', 'Owner');
  store.leave('SAFE', 0, 'wrong');
  assert(store.rooms.get('SAFE').players.length === 1);
});

console.log(`\n${passed} tests passed`);
