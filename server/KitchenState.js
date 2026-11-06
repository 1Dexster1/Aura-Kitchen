const schema = require('@colyseus/schema');
const { Schema, MapSchema, ArraySchema, type } = schema;

class PlayerState extends Schema {
  constructor(slot = 0, name = 'Chef') {
    super();
    this.slot = slot;
    this.name = name;
    this.x = -3 + slot * 2;
    this.z = 0;
    this.rotY = 0;
    this.isMoving = false;
    this.heldItem = '';
  }
}
schema.defineTypes(PlayerState, {
  slot: 'number',
  name: 'string',
  x: 'number',
  z: 'number',
  rotY: 'number',
  isMoving: 'boolean',
  heldItem: 'string'
});

class CustomerState extends Schema {
  constructor(tableIdx = 0, recipeId = 'burger') {
    super();
    this.tableIdx = tableIdx;
    this.recipeId = recipeId;
    this.patience = 45;
    this.maxPatience = 45;
    this.state = 'waiting';
  }
}
schema.defineTypes(CustomerState, {
  tableIdx: 'number',
  recipeId: 'string',
  patience: 'number',
  maxPatience: 'number',
  state: 'string'
});

class KitchenState extends Schema {
  constructor(roomCode = 'AURA') {
    super();
    this.roomCode = roomCode;
    this.isPlaying = false;
    this.shiftSeconds = 120;
    this.totalTips = 0;
    this.platesServed = 0;
    this.comboStreak = 0;
    this.rating = 5.0;
    this.players = new MapSchema();
    this.customers = new ArraySchema();
  }
}
schema.defineTypes(KitchenState, {
  roomCode: 'string',
  isPlaying: 'boolean',
  shiftSeconds: 'number',
  totalTips: 'number',
  platesServed: 'number',
  comboStreak: 'number',
  rating: 'number',
  players: { map: PlayerState },
  customers: [CustomerState]
});

module.exports = {
  PlayerState,
  CustomerState,
  KitchenState
};

