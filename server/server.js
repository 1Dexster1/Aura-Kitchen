const express = require('express');
const http = require('http');
const cors = require('cors');
const path = require('path');
const fs = require('fs');
const { Server } = require('colyseus');
const { monitor } = require('@colyseus/monitor');
const { KitchenRoom } = require('./KitchenRoom');
const { RoomStore, publicRoom } = require('./rooms');

const PORT = process.env.PORT || 30015;
const LEADERBOARD_FILE = path.join(__dirname, 'leaderboard.json');

const app = express();
app.use(cors());
app.use(express.json());

// 1. Serve game static assets directly
app.use(express.static(path.join(__dirname, '..')));

// 2. Colyseus Admin Live Monitor Dashboard
app.use('/colyseus', monitor());

// 3. REST API & Leaderboard
let leaderboard = [];
try {
  if (fs.existsSync(LEADERBOARD_FILE)) {
    leaderboard = JSON.parse(fs.readFileSync(LEADERBOARD_FILE, 'utf8'));
  } else {
    leaderboard = [
      { teamName: 'Aura Master Chefs', tips: 420, plates: 14, rating: 5.0, date: new Date().toISOString() },
      { teamName: 'Ruby & Sapphire Duo', tips: 280, plates: 9, rating: 4.8, date: new Date().toISOString() },
      { teamName: 'Emerald Express', tips: 195, plates: 7, rating: 4.5, date: new Date().toISOString() }
    ];
    fs.writeFileSync(LEADERBOARD_FILE, JSON.stringify(leaderboard, null, 2));
  }
} catch (err) {
  console.error('[Leaderboard] File load error:', err);
}

// Active rooms live in memory; all rules (auth, stale cleanup, rev counter) are in rooms.js
const store = new RoomStore();
setInterval(() => {
  const removed = store.sweep();
  if (removed > 0) console.log(`[Room API] Swept ${removed} silent player(s)`);
}, 2000);

function fail(res, result) {
  return res.status(result.status || 400).json({ success: false, error: result.error });
}

app.get('/api/ping', (req, res) => {
  res.json({ success: true, t: Date.now() });
});

app.post('/api/room/create', (req, res) => {
  const result = store.create(req.body.code, req.body.hostName);
  if (!result.ok) return fail(res, result);
  console.log(`[Room API] Room ${result.room.code} created by ${result.room.host}`);
  res.json({ success: true, code: result.room.code, slot: result.slot, token: result.token, room: publicRoom(result.room) });
});

app.post('/api/room/join', (req, res) => {
  const result = store.join(req.body.code, req.body.playerName);
  if (!result.ok) return fail(res, result);
  const me = result.room.players.find(p => p.slot === result.slot);
  console.log(`[Room API] ${me.name} joined Room ${result.room.code} as slot ${result.slot} (${result.room.players.length}/4)`);
  res.json({ success: true, code: result.room.code, slot: result.slot, token: result.token, room: publicRoom(result.room) });
});

// Optional ?slot=&token= makes this poll count as a heartbeat for lobby guests/hosts.
app.get('/api/room/:code', (req, res) => {
  const slot = req.query.slot;
  const token = typeof req.query.token === 'string' ? req.query.token : undefined;
  const result = store.get(req.params.code, slot, token);
  if (!result.ok) return fail(res, result);
  const room = publicRoom(result.room);
  res.json({ success: true, isPlaying: room.isPlaying, rev: room.rev, players: room.players, room });
});

app.post('/api/room/:code/start', (req, res) => {
  const result = store.start(req.params.code, req.body.slot, req.body.token);
  if (!result.ok) return fail(res, result);
  console.log(`[Room API] Shift started for Room ${result.room.code} with ${result.room.players.length} chefs!`);
  const room = publicRoom(result.room);
  res.json({ success: true, isPlaying: true, rev: room.rev, room });
});

// High-frequency endpoint. Replies with a tiny body when nothing changed since the client's `rev`.
app.post('/api/room/:code/sync', (req, res) => {
  const result = store.sync(req.params.code, req.body);
  if (!result.ok) return fail(res, result);
  if (result.unchanged) {
    return res.json({ success: true, changed: false, rev: result.rev, isPlaying: result.isPlaying, serverTime: Date.now() });
  }
  res.json({
    success: true,
    changed: true,
    rev: result.rev,
    isPlaying: result.isPlaying,
    serverTime: Date.now(),
    players: publicRoom(result.room).players
  });
});

app.post('/api/room/:code/deliver', (req, res) => {
  const result = store.deliver(req.params.code, req.body);
  if (!result.ok) return fail(res, result);
  if (!result.duplicate) {
    const p = result.room.players.find(pl => pl.slot === Number(req.body.slot));
    console.log(`[Room ${result.room.code}] ${p.name} delivered ${result.dishName}! (Dishes: ${p.dishes}, Score: ${p.score})`);
  }
  const room = publicRoom(result.room);
  res.json({ success: true, rev: room.rev, players: room.players, totalDishes: result.room.totalDishes });
});

app.post('/api/room/:code/reset', (req, res) => {
  const result = store.reset(req.params.code, req.body.slot, req.body.token);
  if (!result.ok) return fail(res, result);
  console.log(`[Room API] Room ${result.room.code} reset to lobby state`);
  res.json({ success: true, room: publicRoom(result.room) });
});

app.post('/api/room/:code/leave', (req, res) => {
  store.leave(req.params.code, req.body.slot, req.body.token);
  res.json({ success: true });
});

app.get('/api/status', (req, res) => {
  res.json({
    status: 'online',
    framework: 'Colyseus Game Server',
    game: 'Aura Kitchen',
    version: '1.0.0',
    rooms: store.rooms.size,
    uptime: process.uptime(),
    memoryUsageMB: Math.round(process.memoryUsage().heapUsed / 1024 / 1024)
  });
});

app.get('/api/leaderboard', (req, res) => {
  res.json(leaderboard.slice(0, 10));
});

app.post('/api/score', (req, res) => {
  const { teamName, tips, plates, rating } = req.body;
  if (!teamName || typeof tips !== 'number') {
    return res.status(400).json({ error: 'Invalid score payload' });
  }

  const entry = {
    teamName: String(teamName).substring(0, 24),
    tips: Math.max(0, tips),
    plates: Math.max(0, plates || 0),
    rating: Number(rating) || 5.0,
    date: new Date().toISOString()
  };

  leaderboard.push(entry);
  leaderboard.sort((a, b) => b.tips - a.tips);
  leaderboard = leaderboard.slice(0, 25);

  try {
    fs.writeFileSync(LEADERBOARD_FILE, JSON.stringify(leaderboard, null, 2));
  } catch (err) {
    console.error('[Leaderboard] File write error:', err);
  }

  res.json({ success: true, rank: leaderboard.indexOf(entry) + 1 });
});

// 4. Create HTTP & Colyseus Server
const httpServer = http.createServer(app);
const gameServer = new Server({
  server: httpServer
});

// Define 4-player kitchen room
gameServer.define('kitchen_room', KitchenRoom).filterBy(['roomCode']);

httpServer.listen(PORT, () => {
  console.log(`\n================================================================`);
  console.log(`🍳 Aura Kitchen Colyseus Game Server live on port ${PORT}`);
  console.log(`🌐 Web Game: http://localhost:${PORT}`);
  console.log(`📊 Colyseus Monitor Dashboard: http://localhost:${PORT}/colyseus`);
  console.log(`🏆 Leaderboard API: http://localhost:${PORT}/api/leaderboard`);
  console.log(`🤖 Node.js Discord-Bot Hosting Ready (Zero Native Dependencies)`);
  console.log(`================================================================\n`);
});
