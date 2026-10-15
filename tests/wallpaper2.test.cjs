const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const source = fs.readFileSync(path.join(__dirname, "..", "wallpaper2.js"), "utf8");

function setup(saved) {
	let now = 1000;
	let stored = saved;
	const packets = [];
	const controllers = [];
	const logs = [];
	const context = vm.createContext({
		Date: { now: () => now },
		controller: { name: "Remote wallpaper", ip: "192.168.1.20", port: 8133 },
		device: {
			setName() {}, addFeature() {}, setSize() {}, setFrameRateTarget() {},
			color: () => [10, 20, 30],
		},
		udp: { send: (ip, port, data) => packets.push({ ip, port, data: Array.from(data) }) },
		service: {
			getSetting: () => stored,
			saveSetting: (_group, _key, value) => { stored = value; },
			addController: (controller) => controllers.push(controller),
			getController: (id) => controllers.find((controller) => controller.id === id),
			removeController: (controller) => controllers.splice(controllers.indexOf(controller), 1),
			log: (message) => logs.push(message),
			controllers: [],
		},
	});
	vm.runInContext(source.replace(/^export /gm, ""), context);
	for (const parameter of context.ControllableParameters()) {
		context[parameter.property] = parameter.type === "boolean"
			? parameter.default === "true"
			: parameter.type === "number" ? Number(parameter.default) : parameter.default;
	}
	return {
		context, packets, controllers, logs,
		discovery: new context.DiscoveryService(),
		stored: () => JSON.parse(stored),
		advance: (milliseconds) => { now += milliseconds; },
	};
}

test("adds, persists, reloads and removes LAN endpoints with unique IDs", () => {
	const app = setup();
	assert.equal(app.discovery.addManualDevice("B", " 192.168.1.20 ", "8133"), "");
	assert.equal(app.discovery.addManualDevice("C", "192.168.1.21", "8133"), "");
	assert.equal(app.discovery.addManualDevice("B screen 2", "192.168.1.20", "8134"), "");
	const devices = app.stored();
	assert.equal(new Set(devices.map((device) => device.id)).size, 3);
	assert.equal(devices[0].ip, "192.168.1.20");
	assert.equal(devices[0].port, 8133);
	const reloaded = setup(JSON.stringify(devices));
	reloaded.discovery.initializeManualDevices();
	assert.deepEqual(reloaded.controllers.map(({ ip, port }) => ({ ip, port })),
		devices.map(({ ip, port }) => ({ ip, port })));
	reloaded.discovery.removeManualDevice(devices[0].id);
	assert.equal(reloaded.stored().length, 2);
	assert.equal(reloaded.controllers.length, 2);
});

test("rejects invalid addresses, ports and duplicate endpoints without saving", () => {
	const app = setup("[]");
	for (const ip of ["", "localhost", "192.168.1.256", "192.168.1", "192.168.01.2",
		"0.0.0.0", "224.0.0.1", "255.255.255.255", "::1"]) {
		assert.ok(app.discovery.addManualDevice("B", ip, "8133"));
	}
	for (const port of ["", "0", "-1", "65536", "8133junk", "8133.5", "8133-8136"]) {
		assert.ok(app.discovery.addManualDevice("B", "192.168.1.20", port));
	}
	assert.deepEqual(app.stored(), []);
	assert.equal(app.controllers.length, 0);
	assert.equal(app.logs.length, 16);
	assert.equal(app.discovery.addManualDevice("B", "192.168.1.20", "8133"), "");
	assert.ok(app.discovery.addManualDevice("Duplicate", "192.168.1.20", "08133"));
	assert.equal(app.stored().length, 1);
});

test("preserves default localhost device and explicit empty configuration", () => {
	const local = setup();
	local.discovery.initializeManualDevices();
	assert.equal(local.controllers[0].ip, "127.0.0.1");
	assert.equal(local.controllers[0].port, 8133);
	const empty = setup("[]");
	empty.discovery.initializeManualDevices();
	assert.equal(empty.controllers.length, 0);
});

test("sends settings before RGB, refreshes every five seconds and after initialization", () => {
	const app = setup();
	const { context, packets } = app;
	context.Initialize();
	context.Render();
	assert.deepEqual(packets.map((packet) => packet.data[0]), [1, 0, 0, 0]);
	assert.ok(packets.every((packet) => packet.ip === "192.168.1.20" && packet.port === 8133));
	assert.deepEqual(packets[0].data.slice(0, 18),
		[1, 4, 1, 0, false, 20, 0, 2, 0, 60, 2, 0, 0, 0, 0, 0, 155, 222]);
	const rgb = packets.slice(1);
	assert.deepEqual(rgb.map((packet) => packet.data.slice(0, 3)),
		[[0, 0, 3], [0, 1, 3], [0, 2, 3]]);
	assert.deepEqual(rgb.map((packet) => packet.data.length), [1443, 1443, 1011]);
	assert.deepEqual(rgb.flatMap((packet) => packet.data.slice(3)),
		Array.from({ length: 1296 }, () => [10, 20, 30]).flat());
	packets.length = 0;
	app.advance(4999);
	context.Render();
	assert.ok(packets.every((packet) => packet.data[0] === 0));
	packets.length = 0;
	app.advance(1);
	context.Render();
	assert.equal(packets[0].data[0], 1);
	packets.length = 0;
	context.BlurIntensity = 42;
	context.onBlurIntensityChanged();
	assert.equal(packets[0].data[5], 42);
	packets.length = 0;
	context.Initialize();
	context.Render();
	assert.equal(packets[0].data[0], 1);
	packets.length = 0;
	context.controller.ip = "127.0.0.1";
	context.Shutdown(false);
	assert.ok(packets.every((packet) => packet.ip === "127.0.0.1" && packet.data[0] === 0));
	assert.deepEqual(packets[0].data.slice(3, 6), [0, 155, 222]);
});
