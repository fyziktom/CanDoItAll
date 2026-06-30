(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const agentFramework = root.agentFramework = root.agentFramework || {};

    agentFramework.downloadJson = function (fileName, json) {
        const blob = new Blob([json || ""], { type: "application/json;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");

        anchor.href = url;
        anchor.download = fileName || "agent-thread-history.json";
        anchor.style.display = "none";
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();

        window.setTimeout(function () {
            URL.revokeObjectURL(url);
        }, 0);
    };
})();
