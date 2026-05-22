import { joinSession } from "@github/copilot-sdk/extension";
import { writeFile, mkdir } from "node:fs/promises";
import { resolve, join } from "node:path";
import { existsSync } from "node:fs";

const REPO_ROOT = resolve(import.meta.dirname, "../../..");
const COMPONENTS_DIR = join(REPO_ROOT, "Client", "Components");

function razorTemplate(name) {
    return `<BSModal @ref="Modal" Id="${name.charAt(0).toLowerCase() + name.slice(1)}Modal" IsVerticallyCentered="true" Size="ModalSize.Large">
    <Body>
        <div class="${toKebab(name)}-container">
            <!-- Header integrated into body -->
            <div class="${toKebab(name)}-header">
                <h5 class="${toKebab(name)}-title">
                    ${name}
                </h5>
            </div>

            <!-- TODO: Add content here -->

            <!-- Footer integrated into body -->
            <div class="${toKebab(name)}-footer">
                <button type="button" class="btn btn-secondary" @onclick="() => Modal?.Hide()">
                    Close
                </button>
            </div>
        </div>
    </Body>
</BSModal>

@code {
    public BSModal? Modal;
}
`;
}

function codeTemplate(name) {
    return `namespace BlazorApp.Client.Components;

public partial class ${name}
{
}
`;
}

function cssTemplate(name) {
    const kebab = toKebab(name);
    return `.${kebab}-container {
    color: #ffffff;
    padding: 1.5rem;
    max-height: 80vh;
    overflow-y: auto;
}

.${kebab}-header {
    text-align: center;
    margin-bottom: 1.5rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.${kebab}-title {
    color: #ffffff;
    font-size: 1.5rem;
    font-weight: 700;
    margin: 0;
}

.${kebab}-footer {
    text-align: center;
    margin-top: 1.5rem;
    padding-top: 1rem;
    border-top: 1px solid rgba(255, 255, 255, 0.1);
}
`;
}

function toKebab(name) {
    return name
        .replace(/([A-Z])/g, (m, c, i) => (i > 0 ? "-" : "") + c.toLowerCase())
        .replace(/^-/, "");
}

await joinSession({
    tools: [
        {
            name: "scaffold_modal",
            description:
                "Scaffolds a new Blazor modal component in Client/Components/ following the project's " +
                "design standards: BSModal with modal-body only, dark theme, no modal-header/footer. " +
                "Creates ModalName.razor, ModalName.razor.cs, and ModalName.razor.css. " +
                "The name should be PascalCase ending in 'Modal' (e.g. 'InventoryModal').",
            parameters: {
                type: "object",
                properties: {
                    name: {
                        type: "string",
                        description:
                            "PascalCase component name, should end with 'Modal'. E.g. 'InventoryModal'.",
                    },
                },
                required: ["name"],
            },
            handler: async (args) => {
                const name = args.name.trim();
                if (!name) return "Error: name is required.";
                if (!/^[A-Z][A-Za-z0-9]+$/.test(name))
                    return `Error: name must be PascalCase with no spaces or symbols. Got: "${name}"`;

                const files = [
                    { ext: ".razor", content: razorTemplate(name) },
                    { ext: ".razor.cs", content: codeTemplate(name) },
                    { ext: ".razor.css", content: cssTemplate(name) },
                ];

                const results = [];
                for (const { ext, content } of files) {
                    const path = join(COMPONENTS_DIR, `${name}${ext}`);
                    if (existsSync(path)) {
                        results.push(`⚠️  Skipped (already exists): ${name}${ext}`);
                    } else {
                        await mkdir(COMPONENTS_DIR, { recursive: true });
                        await writeFile(path, content, "utf-8");
                        results.push(`✅ Created: Client/Components/${name}${ext}`);
                    }
                }

                return [
                    `## Scaffold Modal: ${name}`,
                    results.join("\n"),
                    "",
                    "Next steps:",
                    `1. Add content inside the \`.${toKebab(name)}-container\` div in ${name}.razor`,
                    `2. Add any parameters or logic to ${name}.razor.cs`,
                    `3. Register and open the modal from the parent component:`,
                    `   \`<${name} @ref="_${name.charAt(0).toLowerCase() + name.slice(1)}" />\``,
                    `   \`_${name.charAt(0).toLowerCase() + name.slice(1)}?.Modal?.Show();\``,
                ].join("\n");
            },
        },
    ],
});
