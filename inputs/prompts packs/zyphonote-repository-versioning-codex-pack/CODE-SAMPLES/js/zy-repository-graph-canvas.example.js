(function () {
  "use strict";

  function asArray(value) {
    return Array.isArray(value) ? value : [];
  }

  function drawGraph(canvas, graph) {
    if (!canvas || !canvas.getContext) {
      throw new Error("Canvas is required.");
    }

    var context = canvas.getContext("2d");
    var commits = asArray(graph && graph.commits);
    var width = canvas.width;
    var rowHeight = 56;
    var leftPad = 56;

    context.clearRect(0, 0, width, canvas.height);
    context.font = "13px sans-serif";
    context.textBaseline = "middle";

    commits.forEach(function (commit, index) {
      var lane = Number(commit.lane || 0);
      var x = leftPad + (lane * 28);
      var y = 32 + (index * rowHeight);

      context.beginPath();
      context.arc(x, y, 8, 0, Math.PI * 2);
      context.fill();

      context.fillText(String(commit.shortHash || "").slice(0, 7), x + 22, y - 8);
      context.fillText(String(commit.message || ""), x + 22, y + 10);

      asArray(commit.labels).forEach(function (label, labelIndex) {
        context.fillText("[" + label + "]", x + 240 + (labelIndex * 92), y);
      });
    });
  }

  window.ZyRepositoryGraphCanvas = {
    drawGraph: drawGraph
  };
})();
