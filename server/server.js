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
