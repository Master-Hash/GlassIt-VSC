import { workspace, window, commands } from "vscode";
import { setTransparency } from "./index.js";

export function activate(context) {
    const config = () => workspace.getConfiguration("glassit");
    const logger = window.createOutputChannel("GlassIt", { log: true });

    context.subscriptions.push(logger);

    function setAlpha(alpha) {
        if (alpha < 1) {
            alpha = 1;
        } else if (alpha > 255) {
            alpha = 255;
        }

        try {
            const result = setTransparency(process.pid, alpha);
            if (!result) {
                throw new Error("Native transparency call returned false.");
            }

            logger.info(`Set alpha ${alpha}`);
            config().update("alpha", alpha, true);
        } catch (err) {
            logger.error(err);
            window.showErrorMessage(`GlassIt Error: ${err}`);
        }
    }

    logger.info('Extension "GlassIt VSC" is now active.');

    context.subscriptions.push(
        commands.registerCommand("glassit.increase", () => {
            const alpha = config().get("alpha") - config().get("step");
            setAlpha(alpha);
        }),
    );

    context.subscriptions.push(
        commands.registerCommand("glassit.decrease", () => {
            const alpha = config().get("alpha") + config().get("step");
            setAlpha(alpha);
        }),
    );

    context.subscriptions.push(
        commands.registerCommand("glassit.maximize", () => {
            setAlpha(1);
        }),
    );

    context.subscriptions.push(
        commands.registerCommand("glassit.minimize", () => {
            setAlpha(255);
        }),
    );

    const alpha = config().get("alpha");
    setAlpha(alpha);
}

export function deactivate() {}
