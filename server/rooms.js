'use strict';

/**
 * Room logic for Aura Kitchen's REST multiplayer, kept free of express so it can be unit tested.
 *
 * Guarantees:
 *  - a room code can't be hijacked while its room is alive (create -> 409)
 *  - every mutating call needs the per-player token issued at create/join
 *  - players that stop talking are removed (host is promoted, empty rooms are deleted)
 *  - `rev` increases only on real changes, so clients can skip unchanged payloads
 *  - tokens never appear in anything returned to other players
 */

const crypto = require('crypto');

const MAX_PLAYERS = 4;
const LOBBY_STALE_MS = 20000;
const PLAYING_STALE_MS = 30000;
const ROOM_MAX_AGE_MS = 3 * 3600 * 1000;
const CHEF_ROLES = ['Chef Ruby (P1)', 'Chef Sapphire (P2)', 'Chef Emerald (P3)', 'Chef Amber (P4)'];
const MAX_POINTS_PER_DELIVERY = 1000;
const WORLD_LIMIT = 200;

function cleanName(raw, fallback) {
  const s = String(raw == null ? '' : raw)
    .replace(/[\u0000-\u001f\u007f<>]/g, '')
    .trim()
    .slice(0, 16);
  return s || fallback;
}

function cleanCode(raw) {
  const s = String(raw == null ? '' : raw).toUpperCase().replace(/[^A-Z0-9]/g, '');
  return s.length >= 3 && s.length <= 8 ? s : '';
}

function clampNum(v, min, max) {
  return typeof v === 'number' && Number.isFinite(v) ? Math.min(max, Math.max(min, v)) : undefined;
}

function publicPlayer(p) {
  return {
    slot: p.slot,
    name: p.name,
    role: p.role,
    isHost: p.isHost,
    hasPos: !!p.hasPos,
    x: p.x || 0,
    z: p.z || 0,
    rotY: p.rotY || 0,
    isWalking: !!p.isWalking,
    dishes: p.dishes || 0,
    score: p.score || 0
  };
}

function publicRoom(room) {
  return {
    code: room.code,
    host: room.host,
    isPlaying: room.isPlaying,
    rev: room.rev,
    players: room.players.map(publicPlayer)
  };
}

class RoomStore {
  constructor(now) {
    this.now = now || (() => Date.now());
    this.rooms = new Map();
  }

  staleMs(room) {
    return room.isPlaying ? PLAYING_STALE_MS : LOBBY_STALE_MS;
  }

  isAlive(room) {
    const t = this.now();
    return room.players.some(p => t - p.lastSeen <= this.staleMs(room));
  }

  newToken() {
    return crypto.randomBytes(12).toString('hex');
  }

  auth(room, slot, token) {
    if (!room || typeof token !== 'string' || token.length === 0) return null;
    const s = Number(slot);
    const p = room.players.find(pl => pl.slot === s);
    if (!p || room.tokens[s] !== token) return null;
    p.lastSeen = this.now();
    return p;
  }

  create(rawCode, rawHostName) {
    const code = cleanCode(rawCode);
    if (!code) return { ok: false, status: 400, error: 'Room code must be 3-8 letters or digits' };

    const existing = this.rooms.get(code);
    if (existing && this.isAlive(existing)) {
      return { ok: false, status: 409, error: 'Room code already in use' };
    }

    const hostName = cleanName(rawHostName, CHEF_ROLES[0]);
    const t = this.now();
    const token = this.newToken();
    const room = {
      code,
      host: hostName,
      isPlaying: false,
      createdAt: t,
      rev: 1,
      totalDishes: 0,
      tokens: { 0: token },
      players: [
        { slot: 0, name: hostName, role: CHEF_ROLES[0], isHost: true, joinedAt: t, lastSeen: t, lastSeq: 0 }
      ]
    };
    this.rooms.set(code, room);
    return { ok: true, slot: 0, token, room };
  }

  join(rawCode, rawName) {
    const code = cleanCode(rawCode);
    if (!code) return { ok: false, status: 400, error: 'Room code is required' };

    const room = this.rooms.get(code);
    if (!room || !this.isAlive(room)) return { ok: false, status: 404, error: 'Room not found. Check the code!' };
    if (room.isPlaying) return { ok: false, status: 400, error: 'Match already started in this room' };
    if (room.players.length >= MAX_PLAYERS) return { ok: false, status: 400, error: 'Room is full (max 4 players)' };

    const occupied = new Set(room.players.map(p => p.slot));
    let slot = 0;
    while (occupied.has(slot) && slot < MAX_PLAYERS) slot++;

    const t = this.now();
    const token = this.newToken();
    room.tokens[slot] = token;
    room.players.push({
      slot,
      name: cleanName(rawName, CHEF_ROLES[slot]),
      role: CHEF_ROLES[slot],
      isHost: false,
      joinedAt: t,
      lastSeen: t,
      lastSeq: 0
    });
    room.rev++;
    return { ok: true, slot, token, room };
  }

  /** Read-only lookup; a valid slot+token pair doubles as a heartbeat. */
  get(rawCode, slot, token) {
    const room = this.rooms.get(cleanCode(rawCode));
    if (!room) return { ok: false, status: 404, error: 'Room not found' };
    if (token !== undefined) this.auth(room, slot, token);
    return { ok: true, room };
  }

  start(rawCode, slot, token) {
    const room = this.rooms.get(cleanCode(rawCode));
    if (!room) return { ok: false, status: 404, error: 'Room not found' };
    const p = this.auth(room, slot, token);
    if (!p || !p.isHost) return { ok: false, status: 403, error: 'Only the host can start the shift' };

    if (!room.isPlaying) {
      room.isPlaying = true;
      room.startedAt = this.now();
      // Scene loading can take a while: restart every stale clock at the moment the match begins.
      for (const pl of room.players) pl.lastSeen = this.now();
      room.rev++;
    }
    return { ok: true, room };
  }

  sync(rawCode, body) {
    const room = this.rooms.get(cleanCode(rawCode));
    if (!room) return { ok: false, status: 404, error: 'Room not found' };
    body = body || {};
    const p = this.auth(room, body.slot, body.token);
    if (!p) return { ok: false, status: 403, error: 'Not a member of this room' };

    let changed = false;
    const x = clampNum(body.x, -WORLD_LIMIT, WORLD_LIMIT);
    const z = clampNum(body.z, -WORLD_LIMIT, WORLD_LIMIT);
    const rotY = clampNum(body.rotY, -720, 720);

    if (x !== undefined && z !== undefined) {
      if (!p.hasPos || Math.abs((p.x || 0) - x) > 0.005 || Math.abs((p.z || 0) - z) > 0.005) {
        p.x = x;
        p.z = z;
        changed = true;
      }
      p.hasPos = true;
    }
    if (rotY !== undefined && Math.abs((p.rotY || 0) - rotY) > 0.5) {
      p.rotY = rotY;
      changed = true;
    }
    if (typeof body.isWalking === 'boolean' && body.isWalking !== !!p.isWalking) {
      p.isWalking = body.isWalking;
      changed = true;
    }
    if (changed) room.rev++;

    const clientRev = Number(body.rev);
    return {
      ok: true,
      room,
      rev: room.rev,
      isPlaying: room.isPlaying,
      unchanged: Number.isFinite(clientRev) && clientRev === room.rev
    };
  }

  deliver(rawCode, body) {
    const room = this.rooms.get(cleanCode(rawCode));
    if (!room) return { ok: false, status: 404, error: 'Room not found' };
    body = body || {};
    const p = this.auth(room, body.slot, body.token);
    if (!p) return { ok: false, status: 403, error: 'Not a member of this room' };
    if (!room.isPlaying) return { ok: false, status: 400, error: 'Match is not running' };

    const seq = Number(body.seq);
    if (Number.isFinite(seq) && seq > 0) {
      if (seq <= p.lastSeq) return { ok: true, room, duplicate: true }; // retry of an already counted delivery
      p.lastSeq = seq;
    }

    const pts = clampNum(Number(body.earnedPoints), 0, MAX_POINTS_PER_DELIVERY);
    p.dishes = (p.dishes || 0) + 1;
    p.score = (p.score || 0) + (pts === undefined ? 100 : pts);
    room.totalDishes++;
    room.rev++;
    return { ok: true, room, duplicate: false, dishName: cleanName(body.dishName, 'Dish') };
  }

  reset(rawCode, slot, token) {
    const room = this.rooms.get(cleanCode(rawCode));
    if (!room) return { ok: false, status: 404, error: 'Room not found' };
    const p = this.auth(room, slot, token);
    if (!p || !p.isHost) return { ok: false, status: 403, error: 'Only the host can reset the room' };

    room.isPlaying = false;
    room.startedAt = null;
    room.totalDishes = 0;
    for (const pl of room.players) {
      pl.dishes = 0;
      pl.score = 0;
      pl.lastSeq = 0;
      pl.lastSeen = this.now();
    }
    room.rev++;
    return { ok: true, room };
  }

  leave(rawCode, slot, token) {
    const room = this.rooms.get(cleanCode(rawCode));
    if (!room) return { ok: true };
    const p = this.auth(room, slot, token);
    if (!p) return { ok: true }; // nothing to do, and never reveal why
    this.removePlayer(room, p.slot);
    return { ok: true };
  }

  removePlayer(room, slot) {
    const leaving = room.players.find(p => p.slot === slot);
    room.players = room.players.filter(p => p.slot !== slot);
    delete room.tokens[slot];

    if (room.players.length === 0) {
      this.rooms.delete(room.code);
      return;
    }
    if (leaving && leaving.isHost) {
      const next = room.players.slice().sort((a, b) => a.slot - b.slot)[0];
      next.isHost = true;
      room.host = next.name;
    }
    room.rev++;
  }

  /** Drops silent players and dead rooms. Returns how many players were removed. */
  sweep() {
    const t = this.now();
    let removed = 0;
    for (const room of Array.from(this.rooms.values())) {
      if (t - room.createdAt > ROOM_MAX_AGE_MS) {
        this.rooms.delete(room.code);
        continue;
      }
      const limit = this.staleMs(room);
      for (const p of room.players.slice()) {
        if (t - p.lastSeen > limit) {
          this.removePlayer(room, p.slot);
          removed++;
          if (!this.rooms.has(room.code)) break;
        }
      }
    }
    return removed;
  }
}

module.exports = {
  RoomStore,
  publicRoom,
  publicPlayer,
  cleanName,
  cleanCode,
  MAX_PLAYERS,
  LOBBY_STALE_MS,
  PLAYING_STALE_MS
};
