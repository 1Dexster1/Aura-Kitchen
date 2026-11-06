const { Room } = require('colyseus');
const { KitchenState, PlayerState, CustomerState } = require('./KitchenState');

const CHEF_SLOTS = [
  { id: 0, name: 'Chef Ruby', hex: '#ff3344' },
  { id: 1, name: 'Chef Sapphire', hex: '#0088ff' },
  { id: 2, name: 'Chef Emerald', hex: '#00dd66' },
  { id: 3, name: 'Chef Amber', hex: '#ffaa00' }
];

class KitchenRoom extends Room {
  onCreate(options) {
    this.maxClients = 4;
    const code = (options.roomCode || this.generateRoomCode()).toUpperCase();
    this.setState(new KitchenState(code));
    this.setMetadata({ roomCode: code });

    console.log(`[Colyseus] KitchenRoom created with code: ${code}`);

    // High-frequency movement handler
    this.onMessage('move', (client, data) => {
      const player = this.state.players.get(client.sessionId);
      if (player) {
        player.x = data.x;
        player.z = data.z;
        player.rotY = data.rotY;
        player.isMoving = !!data.isMoving;
      }
    });

    // Kitchen counter interaction handler
    this.onMessage('interact', (client, data) => {
      const player = this.state.players.get(client.sessionId);
      if (player && data.heldItem !== undefined) {
        player.heldItem = data.heldItem;
      }
      // Broadcast action to other players
      this.broadcast('counter_interact', {
        sessionId: client.sessionId,
        slot: player ? player.slot : 0,
        counterIdx: data.counterIdx,
        action: data.action,
        item: data.item
      }, { except: client });
    });

    // Knife chopping progress handler
    this.onMessage('chop', (client, data) => {
      const player = this.state.players.get(client.sessionId);
      this.broadcast('chop_action', {
        slot: player ? player.slot : 0,
        counterIdx: data.counterIdx,
        progress: data.progress
      }, { except: client });
    });

    // Shift start trigger
    this.onMessage('start_shift', (client) => {
      if (!this.state.isPlaying) {
        this.state.isPlaying = true;
        this.broadcast('shift_started', { totalChefs: this.state.players.size });
        this.startShiftCountdown();
      }
    });

    // Order delivery handler
    this.onMessage('deliver', (client, data) => {
      const player = this.state.players.get(client.sessionId);
      this.state.totalTips += Number(data.tip) || 15;
      this.state.platesServed++;

      this.broadcast('order_delivered', {
        slot: player ? player.slot : 0,
        tableIdx: data.tableIdx,
        tip: data.tip,
        dishName: data.dishName,
        totalTips: this.state.totalTips,
        platesServed: this.state.platesServed
      });
    });

    // Set simulation tick at 25 FPS
    this.setSimulationInterval((deltaTime) => this.update(deltaTime), 1000 / 25);
  }

  onJoin(client, options) {
    // Find next available chef slot (0 to 3)
    const occupiedSlots = new Set();
    this.state.players.forEach((p) => occupiedSlots.add(p.slot));

    let freeSlot = 0;
    while (occupiedSlots.has(freeSlot) && freeSlot < 4) freeSlot++;

    const chefInfo = CHEF_SLOTS[freeSlot] || CHEF_SLOTS[0];
    const playerName = options.name || chefInfo.name;

    const newPlayer = new PlayerState(freeSlot, playerName);
    this.state.players.set(client.sessionId, newPlayer);

    // Send individual welcome payload
    client.send('joined_info', {
      slot: freeSlot,
      chefInfo: chefInfo,
      roomCode: this.state.roomCode
    });

    console.log(`[Colyseus] ${playerName} joined Room ${this.state.roomCode} as ${chefInfo.name} (${this.state.players.size}/4)`);
  }

  onLeave(client, consented) {
    const player = this.state.players.get(client.sessionId);
    if (player) {
      console.log(`[Colyseus] ${player.name} left Room ${this.state.roomCode}`);
      this.state.players.delete(client.sessionId);
    }
  }

  update(deltaTime) {
    // Authoritative room simulation if shift is playing
    if (this.state.isPlaying) {
      // Periodic customer patience updates or state checks
    }
  }

  startShiftCountdown() {
    this.clock.setInterval(() => {
      if (!this.state.isPlaying) return;
      this.state.shiftSeconds--;
      if (this.state.shiftSeconds <= 0) {
        this.state.isPlaying = false;
        this.broadcast('shift_ended', {
          tips: this.state.totalTips,
          plates: this.state.platesServed
        });
      }
    }, 1000);
  }

  generateRoomCode() {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    let code = '';
    for (let i = 0; i < 4; i++) {
      code += chars[Math.floor(Math.random() * chars.length)];
    }
    return code;
  }
}

module.exports = { KitchenRoom };

