const { spawn, execSync } = require("child_process");
const path = require("path");
const fs = require("fs");
const https = require("https");
const os = require("os");

const token = "eyJhIjoiN2YyMjUzNmU4YTk1ZGI4ZDcxM2U2ZTRmYWU4ZDUzMzIiLCJ0IjoiMTVjYjMxZjItY2ZhMC00YzRhLTlkM2MtMGVjYzQwNmY0YmI0IiwicyI6Ik56Qm1aakUwTUdVdE16ZzBNaTAwWVRGbExXSXlPV0l0WmpOaVlqZGpPR0V4TmpRdyJ9";

console.log("🚀 Starting SKORPPPPP Stack...");

const isWindows = os.platform() === 'win32';
const binaryName = isWindows ? "cloudflared.exe" : "cloudflared";
const cloudflaredPath = path.join(__dirname, binaryName);

async function downloadCloudflared() {
    if (fs.existsSync(cloudflaredPath)) {
        const stats = fs.statSync(cloudflaredPath);
        if (stats.size > 1000000) { 
            return Promise.resolve();
        } else {
            console.log("⚠️ Found corrupted cloudflared binary, downloading again...");
            fs.unlinkSync(cloudflaredPath);
        }
    }
    
    console.log("📥 Downloading cloudflared binary...");
    
    const platform = os.platform(); 
    const arch = os.arch(); 
    
    let url = "";
    if (platform === "linux") {
        url = arch === "arm64" 
            ? "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm64"
            : "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64";
    } else if (platform === "win32") {
        url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";
    } else {
        url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-darwin-amd64";
    }
    
    const downloadFile = (fileUrl, dest) => {
        return new Promise((resolve, reject) => {
            const file = fs.createWriteStream(dest);
            const request = https.get(fileUrl, (response) => {
                if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
                    file.close();
                    console.log(`➡️ Following redirect to: ${response.headers.location}`);
                    resolve(downloadFile(response.headers.location, dest)); 
                } else if (response.statusCode === 200) {
                    response.pipe(file);
                    file.on("finish", () => {
                        file.close();
                        console.log("✅ cloudflared downloaded successfully.");
                        resolve();
                    });
                } else {
                    fs.unlink(dest, () => {});
                    reject(new Error(`Server responded with ${response.statusCode}: ${response.statusMessage}`));
                }
            });

            request.on("error", (err) => {
                file.close();
                fs.unlink(dest, () => {});
                reject(err);
            });
        });
    };

    return downloadFile(url, cloudflaredPath);
}

async function start() {
    try {
        await downloadCloudflared();
        
        try {
            if (!isWindows) {
                execSync(`chmod +x "${cloudflaredPath}"`);
                console.log("✅ cloudflared permissions fixed.");
            }
        } catch (err) {
            console.warn("⚠️ Could not chmod cloudflared:", err.message);
        }

        let cloudflaredProcess = null;
        let shouldRestartTunnel = true;

        function startTunnel() {
            console.log("⚡ Starting Cloudflare Tunnel (Protocol: HTTP/2)...");
            cloudflaredProcess = spawn(cloudflaredPath, [
                "tunnel",
                "run",
                "--protocol",
                "http2",
                "--token",
                token
            ], { stdio: "inherit" });

            cloudflaredProcess.on("error", (err) => {
                console.error("❌ Failed to start cloudflared:", err.message);
            });

            cloudflaredProcess.on("close", (code, signal) => {
                console.log(`ℹ️ cloudflared tunnel process exited (code: ${code}, signal: ${signal})`);
                if (shouldRestartTunnel) {
                    console.log("🔄 Reconnecting Cloudflare Tunnel in 3 seconds...");
                    setTimeout(startTunnel, 3000);
                }
            });
        }

        startTunnel();

        console.log("📱 Starting Hosting Dashboard (server.js)...");
        const dashboard = spawn("node", ["index.js"], {
            stdio: "inherit",
            env: {
                ...process.env,
                PORT: process.env.PORT || "30015",
                NODE_ENV: process.env.NODE_ENV || "production",
            },
        });

        dashboard.on("error", (err) => {
            console.error("❌ Failed to start Dashboard:", err.message);
        });

        dashboard.on("close", (code) => {
            console.log(`❌ Dashboard exited with code ${code}`);
            shouldRestartTunnel = false;
            if (cloudflaredProcess) cloudflaredProcess.kill();
            process.exit(code);
        });

        process.on("SIGTERM", () => {
            console.log("Stopping processes...");
            shouldRestartTunnel = false;
            if (cloudflaredProcess) cloudflaredProcess.kill();
            if (dashboard) dashboard.kill();
        });

        process.on("SIGINT", () => {
            console.log("Stopping processes...");
            shouldRestartTunnel = false;
            if (cloudflaredProcess) cloudflaredProcess.kill();
            if (dashboard) dashboard.kill();
            process.exit();
        });

    } catch (error) {
        console.error("❌ Setup failed:", error.message);
    }
}

start();
