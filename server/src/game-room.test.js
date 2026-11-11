"use strict";

const test = require("node:test");
const assert = require("node:assert/strict");
const { GameRoom, ROOM_CAPACITY } = require("./game-room");

function player(userId, team = 1) {
    return {
        userId: String(userId),
        playerName: `Pilot${userId}`,
        team
    };
}

test("room capacity supports 32 players and rejects the next join", () => {
    const room = new GameRoom("room-1");
    for (let id = 1; id <= ROOM_CAPACITY; id++) {
        room.addPlayer(player(id, id % 2 === 0 ? 2 : 1));
    }

    assert.equal(room.snapshot().players.length, 32);
    assert.throws(() => room.addPlayer(player(33)), /full/);
});

test("room enforces team selection and one active connection per account", () => {
    const room = new GameRoom("room-2");
    room.addPlayer(player(1, 2));
    assert.equal(room.snapshot().players[0].team, 2);
    assert.throws(() => room.addPlayer(player(1)), /already connected/);
    assert.throws(
        () => room.addPlayer({ ...player(2), team: 3 }),
        /invalid/
    );
});

test("server clamps movement input and keeps players inside the arena", () => {
    const room = new GameRoom("room-3");
    room.addPlayer(player(1));
    assert.equal(room.setInput("1", {
        throttle: 500,
        steering: 0,
        fire: false
    }), true);

    for (let i = 0; i < 200; i++) {
        room.tick(0.1, 10_000 + i * 100);
    }

    const state = room.snapshot().players[0];
    assert.ok(state.x <= 100 && state.x >= -100);
    assert.ok(state.z <= 100 && state.z >= -100);
    assert.equal(room.setInput("1", {
        throttle: NaN,
        steering: 0,
        fire: false
    }), false);
});

test("server-side projectiles damage only enemies and award a kill", () => {
    const room = new GameRoom("room-4");
    room.addPlayer(player(1, 1));
    room.addPlayer(player(2, 2));
    const shooter = room.players.get("1");
    const target = room.players.get("2");
    shooter.x = 0;
    shooter.z = 0;
    shooter.heading = 90;
    target.x = 10;
    target.z = 0;

    let now = 10_000;
    for (let shot = 0; shot < 4; shot++) {
        room.setInput("1", {
            throttle: 0,
            steering: 0,
            fire: true
        }, now);
        for (let tick = 0; tick < 50; tick++) {
            room.tick(0.05, now + tick * 50);
        }
        now += 2000;
        room.setInput("1", {
            throttle: 0,
            steering: 0,
            fire: false
        }, now);
    }

    assert.equal(target.health, 0);
    assert.equal(target.alive, false);
    assert.equal(shooter.kills, 1);
    assert.equal(target.deaths, 1);

    room.tick(0.05, target.respawnAt);
    assert.equal(target.alive, true);
    assert.equal(target.health, 100);
});

test("a disconnected player and their projectiles are removed", () => {
    const room = new GameRoom("room-5");
    room.addPlayer(player(1));
    room.setInput("1", {
        throttle: 0,
        steering: 0,
        fire: true
    }, 10_000);
    assert.equal(room.projectiles.length, 1);
    assert.equal(room.removePlayer("1"), true);
    assert.equal(room.removePlayer("1"), false);
    assert.equal(room.projectiles.length, 0);
    assert.equal(room.snapshot().players.length, 0);
});
