import { joinSession } from "@github/copilot-sdk/extension";
import { execFile } from "node:child_process";
import { resolve } from "node:path";

const REPO_ROOT = resolve(import.meta.dirname, "../../..");

function run(cmd, args, cwd) {
    return new Promise((resolve_) => {
        const isWin = process.platform === "win32";
        execFile(
            isWin ? "powershell" : cmd,
            isWin ? ["-NoProfile", "-Command", `${cmd} ${args.join(" ")}`] : args,
            { cwd },
            (err, stdout, stderr) => {
                const out = (stdout + (stderr ? `\nSTDERR:\n${stderr}` : "")).trim();
                resolve_({ ok: !err, output: out });
            }
        );
    });
}

await joinSession({
    tools: [
        {
            name: "build_client",
            description:
                "Builds the Blazor Client project (or the full EMEAOnline solution). " +
                "Pass format:true to also run dotnet format before building. " +
                "Returns build output with error/warning summary.",
            parameters: {
                type: "object",
                properties: {
                    target: {
                        type: "string",
                        enum: ["client", "solution"],
                        description: "Build just the Client project (default) or the full solution.",
                        default: "client",
                    },
                    format: {
                        type: "boolean",
                        description: "Run dotnet format before building (default: false).",
                        default: false,
                    },
                    release: {
                        type: "boolean",
                        description: "Build in Release configuration (default: false = Debug).",
                        default: false,
                    },
                },
                required: [],
            },
            handler: async (args) => {
                const target = args.target ?? "client";
                const doFormat = args.format ?? false;
                const config = args.release ? "Release" : "Debug";
                const projectArg =
                    target === "solution"
                        ? "EMEAOnline.slnx"
                        : "Client/Client.csproj";

                const steps = [];

                if (doFormat) {
                    const fmt = await run("dotnet", ["format", "EMEAOnline.slnx"], REPO_ROOT);
                    steps.push(`## dotnet format\n${fmt.output}`);
                    if (!fmt.ok) return steps.join("\n\n") + "\n\nFormat failed — build aborted.";
                }

                const build = await run(
                    "dotnet",
                    ["build", projectArg, "--configuration", config, "--no-restore"],
                    REPO_ROOT
                );
                steps.push(`## dotnet build ${projectArg} (${config})\n${build.output}`);

                return steps.join("\n\n");
            },
        },
    ],
});
