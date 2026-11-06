const express = require('express');
const http = require('http');
const cors = require('cors');
const path = require('path');
const fs = require('fs');
const { Server } = require('colyseus');
const { monitor } = require('@colyseus/monitor');
const { KitchenRoom } = require('./KitchenRoom');

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

// Active Rooms In-Memory Store
const activeRooms = new Map();

setInterval(() => {
  const now = Date.now();
  for (const [code, room] of activeRooms.entries()) {
    if (now - room.createdAt > 3 * 3600 * 1000) {
      activeRooms.delete(code);
    }
  }
}, 60000);

app.post('/api/room/create', (req, res) => {
  const code = (req.body.code || 'AUR4').toUpperCase().trim();
  const hostName = (req.body.hostName || 'Chef Ruby').trim();

  const room = {
    code,
    host: hostName,
    isPlaying: false,
    createdAt: Date.now(),
    players: [
      { slot: 0, name: hostName, role: 'Chef Ruby (P1)', isHost: true, joinedAt: Date.now() }
    ]
  };

  activeRooms.set(code, room);
  console.log(`[Room API] Room ${code} created by ${hostName}`);
  res.json({ success: true, code, slot: 0, room });
});

app.post('/api/room/join', (req, res) => {
  const code = (req.body.code || '').toUpperCase().trim();
  const playerName = (req.body.playerName || '').trim();

  if (!code) {
    return res.status(400).json({ success: false, error: 'Room code is required' });
  }

  const room = activeRooms.get(code);
  if (!room) {
    return res.status(404).json({ success: false, error: 'Room not found. Check the code!' });
  }

  if (room.isPlaying) {
    return res.status(400).json({ success: false, error: 'Match already started in this room' });
  }

  if (room.players.length >= 4) {
    return res.status(400).json({ success: false, error: 'Room is full (max 4 players)' });
  }

  const occupied = new Set(room.players.map(p => p.slot));
  let slot = 0;
  while (occupied.has(slot) && slot < 4) slot++;

  const chefNames = ['Chef Ruby (P1)', 'Chef Sapphire (P2)', 'Chef Emerald (P3)', 'Chef Amber (P4)'];
  const assignedName = playerName || chefNames[slot];

  const player = {
    slot,
    name: assignedName,
    role: chefNames[slot],
    isHost: false,
    joinedAt: Date.now()
  };

  room.players.push(player);
  console.log(`[Room API] ${assignedName} joined Room ${code} as slot ${slot} (${room.players.length}/4)`);
  res.json({ success: true, code, slot, room });
});

app.get('/api/room/:code', (req, res) => {
  const code = req.params.code.toUpperCase().trim();
  const room = activeRooms.get(code);
  if (!room) {
    return res.status(404).json({ success: false, error: 'Room not found' });
  }
  res.json({ success: true, room });
});

app.post('/api/room/:code/start', (req, res) => {
  const code = req.params.code.toUpperCase().trim();
  const room = activeRooms.get(code);
  if (!room) {
    return res.status(404).json({ success: false, error: 'Room not found' });
  }

  room.isPlaying = true;
  room.startedAt = Date.now();
  console.log(`[Room API] Shift started for Room ${code} with ${room.players.length} chefs!`);
  res.json({ success: true, isPlaying: true, room });
});

app.post('/api/room/:code/leave', (req, res) => {
  const code = req.params.code.toUpperCase().trim();
  const slot = Number(req.body.slot);
  const room = activeRooms.get(code);
  if (room) {
    room.players = room.players.filter(p => p.slot !== slot);
    if (room.players.length === 0) {
      activeRooms.delete(code);
    }
  }
  res.json({ success: true });
});

app.get('/api/status', (req, res) => {
  res.json({
    status: 'online',
    framework: 'Colyseus Game Server',
    game: 'Aura Kitchen',
    version: '1.0.0',
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
