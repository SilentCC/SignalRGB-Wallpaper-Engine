const assert = require("node:assert/strict");
const { execFileSync } = require("node:child_process");
const fs = require("node:fs");
const path = require("node:path");
const { performance } = require("node:perf_hooks");
const vm = require("node:vm");

const root = path.join(__dirname, "..");
const baseline = execFileSync("git", ["show", `${process.argv[2] || "60610c8"}:wallpaper2.js`],
	{ cwd: root, encoding: "utf8" });
const current = fs.readFileSync(path.join(root, "wallpaper2.js"), "utf8");

function load(source, aspect) {
	let checksum = 0;
	let packets = 0;
	const color = [10, 20, 30];
	const context = vm.createContext({
		controller: { ip: "127.0.0.1", port: 8133 },
		device: { color: () => color },
		udp: { send: (_ip, _port, packet) => {
			checksum += packet.length + packet[1] + packet[3] + packet[packet.length - 1];
			packets++;
		} },
		MatrixSize: aspect, MatrixTier: "X Large", LightingMode: "Canvas",
	});
	vm.runInContext(source.replace(/^export /gm, ""), context);
	return { frame: context.grabColors, totals: () => [checksum, packets] };
}

const reports = [];
for (const aspect of ["16:9 Landscape", "21:9 Portrait"]) {
	const before = load(baseline, aspect);
	const after = load(current, aspect);
	const timings = [[], []];
	for (let i = 0; i < 30; i++) { before.frame(); after.frame(); }
	for (let round = 0; round < 7; round++) {
		for (const index of round % 2 ? [1, 0] : [0, 1]) {
			const app = index ? after : before;
			const start = performance.now();
			for (let frame = 0; frame < 100; frame++) app.frame();
			timings[index].push((performance.now() - start) / 100);
		}
	}
	assert.deepEqual(after.totals(), before.totals());
	const median = (values) => values.sort((a, b) => a - b)[Math.floor(values.length / 2)];
	const oldMs = median(timings[0]);
	const newMs = median(timings[1]);
	reports.push({ aspect, tier: "X Large", baseline_ms_per_frame: +oldMs.toFixed(3),
		current_ms_per_frame: +newMs.toFixed(3), speedup: +(oldMs / newMs).toFixed(2) });
}
console.log(JSON.stringify({
	caveat: "Offline Node.js packet-construction benchmark with a mock canvas and no network IO; not SignalRGB or B display FPS.",
	reports,
}, null, 2));
