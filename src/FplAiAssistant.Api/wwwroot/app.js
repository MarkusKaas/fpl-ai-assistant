// Small vanilla-JS frontend for the FPL AI Assistant API — no build step,
// no framework, just fetch() against the endpoints mapped in Program.cs.
// Kept deliberately simple to match the rest of the project's "weekend MVP" scope.

const DEFAULT_TEAM_ID = "153502";
const STORAGE_KEY = "fpl-ai-assistant:last-team-id";

const teamIdInput = document.getElementById("team-id");
const loadTeamBtn = document.getElementById("load-team-btn");
const refreshDataBtn = document.getElementById("refresh-data-btn");
const statusEl = document.getElementById("status");
const dashboardEl = document.getElementById("dashboard");

function rememberedTeamId() {
  try {
    return localStorage.getItem(STORAGE_KEY) || DEFAULT_TEAM_ID;
  } catch {
    return DEFAULT_TEAM_ID;
  }
}

teamIdInput.value = rememberedTeamId();

function setStatus(message, kind) {
  statusEl.textContent = message || "";
  statusEl.className = "status" + (kind ? " " + kind : "");
}

function setBusy(busy) {
  loadTeamBtn.disabled = busy;
  refreshDataBtn.disabled = busy;
}

async function refreshPlayerData() {
  setBusy(true);
  setStatus("Refreshing player data from the FPL API…");
  try {
    const res = await fetch("/api/data/refresh", { method: "POST" });
    if (!res.ok) throw new Error("Refresh failed (" + res.status + ")");
    const body = await res.json();
    setStatus(`Loaded ${body.playersWritten} players across ${body.teamsWritten} teams.`, "ok");
  } catch (err) {
    setStatus("Couldn't refresh player data: " + err.message, "error");
  } finally {
    setBusy(false);
  }
}

async function loadTeam() {
  const teamId = teamIdInput.value.trim();
  if (!teamId) {
    setStatus("Enter your FPL team ID first.", "error");
    return;
  }

  try {
    localStorage.setItem(STORAGE_KEY, teamId);
  } catch {
    // Storage can be unavailable (e.g. private browsing) — not worth failing over.
  }

  setBusy(true);
  setStatus("Loading your team…");
  dashboardEl.classList.add("hidden");

  try {
    const res = await fetch(`/api/team/${encodeURIComponent(teamId)}`);
    const body = await res.json();

    if (!res.ok) {
      throw new Error(body?.error || `Request failed (${res.status})`);
    }

    renderDashboard(body);
    dashboardEl.classList.remove("hidden");
    setStatus(`Loaded as of gameweek ${body.asOfGameweek}.`, "ok");
  } catch (err) {
    setStatus(err.message, "error");
  } finally {
    setBusy(false);
  }
}

function renderDashboard(data) {
  renderSummary(data);
  renderCaptain(data.suggestedCaptain);
  renderTransfers(data.transferSuggestions);
  renderSquad(data.squad);
  renderLineup(data.recommendedLineup);
  loadTeamHistory(data.teamId);
}

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function renderSummary(data) {
  const grid = document.getElementById("summary-grid");
  grid.innerHTML = "";

  const tiles = [
    ["Manager", `${data.managerName} — ${data.teamName}`],
    ["Overall points", data.overallPoints],
    ["Overall rank", data.overallRank ? data.overallRank.toLocaleString() : "—"],
    ["Gameweek points", data.gameweekPoints],
    ["Squad value", `£${data.teamValue.toFixed(1)}m`],
    ["In the bank", `£${data.bank.toFixed(1)}m`],
  ];

  for (const [label, value] of tiles) {
    const tile = el("div", "summary-tile");
    tile.appendChild(el("div", "label", label));
    tile.appendChild(el("div", "value", String(value)));
    grid.appendChild(tile);
  }
}

function renderCaptain(captain) {
  const content = document.getElementById("captain-content");
  content.innerHTML = "";

  if (!captain) {
    content.appendChild(el("p", "empty-note", "No captain suggestion available."));
    return;
  }

  const row = el("div", "captain-row");
  row.appendChild(el("div", "captain-badge", "C"));

  const text = el("div");
  text.appendChild(el("div", "player-name", `${captain.name} (${captain.team})`));
  text.appendChild(el(
    "div",
    "player-meta",
    `Form ${captain.form} · ${captain.totalPoints} pts this season · fixture ease ${captain.fixtureScore}/5`
  ));
  row.appendChild(text);
  content.appendChild(row);
}

function renderTransfers(suggestions) {
  const content = document.getElementById("transfers-content");
  content.innerHTML = "";

  if (!suggestions || suggestions.length === 0) {
    content.appendChild(el("p", "empty-note", "No transfer suggestions right now — your weakest starters don't have a clearly better, affordable replacement."));
    return;
  }

  for (const s of suggestions) {
    const row = el("div", "transfer-row");

    const outSide = el("div", "transfer-side out");
    outSide.appendChild(el("div", "tag", "Out"));
    outSide.appendChild(el("div", "name", `${s.out.name} (${s.out.team})`));
    row.appendChild(outSide);

    row.appendChild(el("div", "transfer-arrow", "→"));

    const inSide = el("div", "transfer-side in");
    inSide.appendChild(el("div", "tag", "In"));
    inSide.appendChild(el("div", "name", `${s.in.name} (${s.in.team})`));
    row.appendChild(inSide);

    row.appendChild(el("div", "transfer-reason", s.reason));

    content.appendChild(row);
  }
}

function fixtureChip(fixture) {
  const chip = el("span", `fixture-chip fdr-${fixture.difficulty}`);
  chip.textContent = `${fixture.isHome ? "vs" : "@"} ${fixture.opponent} (${fixture.difficulty})`;
  return chip;
}

function playerCard(player) {
  const card = el("div", "player-card" + (player.isCaptain ? " is-captain" : ""));
  if (player.isCaptain) card.appendChild(el("div", "cap-tag", "CAPTAIN"));
  else if (player.isViceCaptain) card.appendChild(el("div", "cap-tag", "VICE"));

  const nameRow = el("div", "player-name-row");
  nameRow.appendChild(el("div", "player-name", player.name));
  nameRow.appendChild(el("div", "player-team", `${player.team} · ${player.position.slice(0, 3).toUpperCase()}`));
  card.appendChild(nameRow);

  card.appendChild(el(
    "div",
    "player-meta",
    `£${player.price.toFixed(1)}m · Form ${player.form} · ${player.totalPoints} pts`
  ));

  const chips = el("div", "fixture-chips");
  if (player.nextFixtures.length === 0) {
    chips.appendChild(el("span", "empty-note", "No upcoming fixtures loaded"));
  } else {
    for (const f of player.nextFixtures) {
      chips.appendChild(fixtureChip(f));
    }
  }
  card.appendChild(chips);

  return card;
}

function renderSquad(squad) {
  const startingGrid = document.getElementById("starting-grid");
  const benchGrid = document.getElementById("bench-grid");
  startingGrid.innerHTML = "";
  benchGrid.innerHTML = "";

  for (const player of squad) {
    const card = playerCard(player);
    (player.isStarting ? startingGrid : benchGrid).appendChild(card);
  }
}

function renderLineup(lineup) {
  const card = document.getElementById("lineup-card");
  const formationBadge = document.getElementById("lineup-formation");
  const changesEl = document.getElementById("lineup-changes");
  const startingGrid = document.getElementById("lineup-starting-grid");
  const benchGrid = document.getElementById("lineup-bench-grid");

  if (!lineup) {
    card.classList.add("hidden");
    return;
  }
  card.classList.remove("hidden");

  formationBadge.textContent = lineup.formation;

  changesEl.innerHTML = "";
  if (!lineup.changesFromCurrent || lineup.changesFromCurrent.length === 0) {
    changesEl.appendChild(el("p", "empty-note", "Your current starting XI already matches the best available formation — no changes suggested."));
  } else {
    const list = el("ul", "changes-list");
    for (const change of lineup.changesFromCurrent) {
      list.appendChild(el("li", null, change));
    }
    changesEl.appendChild(list);
  }

  startingGrid.innerHTML = "";
  for (const player of lineup.starters) {
    startingGrid.appendChild(playerCard(player));
  }

  benchGrid.innerHTML = "";
  for (const player of lineup.bench) {
    benchGrid.appendChild(playerCard(player));
  }
}

// --- League comparison ---------------------------------------------------

const LEAGUE_STORAGE_KEY = "fpl-ai-assistant:last-league-id";
const leagueIdInput = document.getElementById("league-id");
const loadLeagueBtn = document.getElementById("load-league-btn");
const leagueStatusEl = document.getElementById("league-status");
const leagueDashboardEl = document.getElementById("league-dashboard");

try {
  const rememberedLeagueId = localStorage.getItem(LEAGUE_STORAGE_KEY);
  if (rememberedLeagueId) leagueIdInput.value = rememberedLeagueId;
} catch {
  // Storage can be unavailable — not worth failing over.
}

function setLeagueStatus(message, kind) {
  leagueStatusEl.textContent = message || "";
  leagueStatusEl.className = "status" + (kind ? " " + kind : "");
}

async function loadLeague() {
  const leagueId = leagueIdInput.value.trim();
  if (!leagueId) {
    setLeagueStatus("Enter a league ID first.", "error");
    return;
  }

  try {
    localStorage.setItem(LEAGUE_STORAGE_KEY, leagueId);
  } catch {
    // Storage can be unavailable — not worth failing over.
  }

  loadLeagueBtn.disabled = true;
  setLeagueStatus("Loading league…");
  leagueDashboardEl.classList.add("hidden");

  try {
    const yourTeamId = teamIdInput.value.trim();
    const url = yourTeamId
      ? `/api/league/${encodeURIComponent(leagueId)}?highlightTeamId=${encodeURIComponent(yourTeamId)}`
      : `/api/league/${encodeURIComponent(leagueId)}`;
    const res = await fetch(url);
    const body = await res.json();

    if (!res.ok) {
      throw new Error(body?.error || `Request failed (${res.status})`);
    }

    renderLeague(body);
    leagueDashboardEl.classList.remove("hidden");
    setLeagueStatus(`Loaded as of gameweek ${body.asOfGameweek}.`, "ok");
    loadLeagueHistory(leagueId);
  } catch (err) {
    setLeagueStatus(err.message, "error");
  } finally {
    loadLeagueBtn.disabled = false;
  }
}

function renderLeague(data) {
  document.getElementById("league-name").textContent = data.leagueName;

  const highlightsGrid = document.getElementById("league-highlights");
  highlightsGrid.innerHTML = "";
  const highlightTiles = [
    ["Best this gameweek", data.topGameweekScorer ? `${data.topGameweekScorer.managerName} (${data.topGameweekScorer.gameweekPoints} pts)` : "—"],
    ["Most valuable squad", data.mostValuableSquad ? `${data.mostValuableSquad.managerName} (£${data.mostValuableSquad.teamValue.toFixed(1)}m)` : "—"],
    ["Biggest climber", data.biggestClimber ? `${data.biggestClimber.managerName} (+${data.biggestClimber.rankChange})` : "—"],
  ];
  for (const [label, value] of highlightTiles) {
    const tile = el("div", "summary-tile");
    tile.appendChild(el("div", "label", label));
    tile.appendChild(el("div", "value", String(value)));
    highlightsGrid.appendChild(tile);
  }

  const tbody = document.getElementById("league-table-body");
  tbody.innerHTML = "";
  for (const entry of data.entries) {
    const row = document.createElement("tr");
    if (entry.teamId === data.viewerTeamId) row.className = "is-you";

    const rankCell = el("td", null, String(entry.rank));
    if (entry.rankChange && entry.rankChange !== 0) {
      const arrow = el("span", entry.rankChange > 0 ? "rank-up" : "rank-down", entry.rankChange > 0 ? ` ▲${entry.rankChange}` : ` ▼${Math.abs(entry.rankChange)}`);
      rankCell.appendChild(arrow);
    }

    row.appendChild(rankCell);
    row.appendChild(el("td", null, entry.managerName));
    row.appendChild(el("td", null, entry.teamName));
    row.appendChild(el("td", null, String(entry.gameweekPoints)));
    row.appendChild(el("td", null, String(entry.total)));
    row.appendChild(el("td", null, `£${entry.teamValue.toFixed(1)}m`));
    row.appendChild(el("td", null, `£${entry.bank.toFixed(1)}m`));
    row.appendChild(el("td", null, entry.captainName || "—"));

    tbody.appendChild(row);
  }
}

// --- Gameweek history charts ----------------------------------------------
// Hand-rolled SVG bar/line charts — no charting library, matching the rest of
// this frontend's "no build step, no dependencies" scope. Non-critical: a
// failed history fetch just leaves the chart cards showing an empty note,
// it never blocks the main team/league dashboards above.

const SVG_NS = "http://www.w3.org/2000/svg";
const CHART_COLORS = ["#2e7d8c", "#c62828", "#c98a00", "#2e7d32", "#6a4fb3", "#b3467c", "#0f6674", "#7a5230", "#455a64", "#ad1457"];

function svgEl(tag, attrs = {}) {
  const node = document.createElementNS(SVG_NS, tag);
  for (const [key, value] of Object.entries(attrs)) {
    node.setAttribute(key, value);
  }
  return node;
}

// Attaches a native SVG <title>, which browsers show as a plain hover
// tooltip with no extra JS/CSS needed — used as a screen-reader/no-JS
// fallback alongside the custom floating tooltip below (native title
// tooltips are slow to appear and easy to miss, so they're not enough
// on their own).
function svgTitle(node, text) {
  const title = document.createElementNS(SVG_NS, "title");
  title.textContent = text;
  node.appendChild(title);
  return node;
}

// One floating tooltip div per chart container (reused across hovers),
// positioned from the raw mouse event so it tracks the cursor correctly
// no matter how the SVG has been scaled to fit the container.
function ensureChartTooltip(container) {
  let tip = container.querySelector(":scope > .chart-tooltip");
  if (!tip) {
    tip = el("div", "chart-tooltip");
    container.appendChild(tip);
  }
  return tip;
}

function positionChartTooltip(container, tip, evt) {
  const rect = container.getBoundingClientRect();
  const x = evt.clientX - rect.left + container.scrollLeft;
  const y = evt.clientY - rect.top;
  tip.style.left = `${x}px`;
  tip.style.top = `${Math.max(y, 0)}px`;
}

// Wires up a hover/focus tooltip on an SVG point/label showing exact
// values — the "hold the mouse over a point and see the number" feature.
// Works with touch too: tapping a point shows the tooltip, tapping
// elsewhere hides it.
function attachChartTooltip(container, node, text) {
  const tip = ensureChartTooltip(container);
  node.style.cursor = "default";
  node.setAttribute("tabindex", "0");

  const show = (evt) => {
    tip.textContent = text;
    tip.classList.add("is-visible");
    positionChartTooltip(container, tip, evt);
  };
  const move = (evt) => positionChartTooltip(container, tip, evt);
  const hide = () => tip.classList.remove("is-visible");

  node.addEventListener("mouseenter", show);
  node.addEventListener("mousemove", move);
  node.addEventListener("mouseleave", hide);
  node.addEventListener("focus", show);
  node.addEventListener("blur", hide);
  node.addEventListener("touchstart", (evt) => {
    const touch = evt.touches[0];
    if (touch) show({ clientX: touch.clientX, clientY: touch.clientY });
  }, { passive: true });
}

async function loadTeamHistory(teamId) {
  const pointsContainer = document.getElementById("history-points-chart");
  const rankContainer = document.getElementById("history-rank-chart");
  try {
    const res = await fetch(`/api/team/${encodeURIComponent(teamId)}/history`);
    if (!res.ok) throw new Error("history request failed");
    const body = await res.json();
    renderPointsBarChart(pointsContainer, body.history);
    renderRankLineChart(rankContainer, body.history);
  } catch {
    // Non-critical — leave the cards showing their built-in empty state.
    renderPointsBarChart(pointsContainer, []);
    renderRankLineChart(rankContainer, []);
  }
}

async function loadLeagueHistory(leagueId) {
  const container = document.getElementById("league-history-chart");
  const legend = document.getElementById("league-history-legend");
  try {
    const res = await fetch(`/api/league/${encodeURIComponent(leagueId)}/history`);
    if (!res.ok) throw new Error("league history request failed");
    const body = await res.json();
    renderLeagueHistoryChart(container, legend, body.series);
  } catch {
    renderLeagueHistoryChart(container, legend, []);
  }
}

function renderPointsBarChart(container, history) {
  container.innerHTML = "";
  if (!history || history.length === 0) {
    container.appendChild(el("p", "empty-note", "No gameweek history yet."));
    return;
  }

  const width = Math.max(360, history.length * 46);
  const height = 200;
  const padding = { top: 18, right: 12, bottom: 26, left: 12 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;

  const maxPoints = Math.max(...history.map((h) => h.points), 10);
  const barWidth = chartWidth / history.length;

  const svg = svgEl("svg", { viewBox: `0 0 ${width} ${height}`, class: "chart-svg", role: "img", "aria-label": "Points per gameweek" });
  svg.appendChild(svgEl("line", {
    x1: padding.left, y1: height - padding.bottom, x2: width - padding.right, y2: height - padding.bottom,
    stroke: "#d8dee6", "stroke-width": 1,
  }));

  history.forEach((h, i) => {
    const barHeight = maxPoints > 0 ? (h.points / maxPoints) * chartHeight : 0;
    const x = padding.left + i * barWidth + barWidth * 0.15;
    const y = height - padding.bottom - barHeight;
    const w = barWidth * 0.7;

    const bar = svgEl("rect", { x, y, width: w, height: Math.max(barHeight, 0), rx: 3, fill: "#2e7d8c" });
    const barTooltip = `GW${h.gameweek}: ${h.points} pts`;
    svgTitle(bar, barTooltip);
    attachChartTooltip(container, bar, barTooltip);
    svg.appendChild(bar);

    const valueLabel = svgEl("text", { x: x + w / 2, y: y - 4, "text-anchor": "middle", class: "chart-value-label" });
    valueLabel.textContent = h.points;
    svg.appendChild(valueLabel);

    const axisLabel = svgEl("text", { x: x + w / 2, y: height - padding.bottom + 14, "text-anchor": "middle", class: "chart-axis-label" });
    axisLabel.textContent = "GW" + h.gameweek;
    svg.appendChild(axisLabel);
  });

  container.appendChild(svg);
}

function renderRankLineChart(container, history) {
  container.innerHTML = "";
  const ranked = (history || []).filter((h) => h.overallRank != null);
  if (ranked.length === 0) {
    container.appendChild(el("p", "empty-note", "No overall-rank history yet."));
    return;
  }

  const width = Math.max(360, ranked.length * 46);
  const height = 180;
  const padding = { top: 16, right: 16, bottom: 26, left: 64 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;

  const minRank = Math.min(...ranked.map((h) => h.overallRank));
  const maxRank = Math.max(...ranked.map((h) => h.overallRank));
  const rankSpan = Math.max(maxRank - minRank, 1);

  const xFor = (i) => padding.left + (i / Math.max(ranked.length - 1, 1)) * chartWidth;
  const yFor = (rank) => padding.top + ((rank - minRank) / rankSpan) * chartHeight;

  const svg = svgEl("svg", { viewBox: `0 0 ${width} ${height}`, class: "chart-svg", role: "img", "aria-label": "Overall rank over time" });

  const linePoints = ranked.map((h, i) => `${xFor(i)},${yFor(h.overallRank)}`).join(" ");
  svg.appendChild(svgEl("polyline", { points: linePoints, fill: "none", stroke: "#1f2a44", "stroke-width": 2 }));

  ranked.forEach((h, i) => {
    const point = svgEl("circle", { cx: xFor(i), cy: yFor(h.overallRank), r: 3, fill: "#1f2a44" });
    const pointTooltip = `GW${h.gameweek}: rank ${h.overallRank.toLocaleString()}`;
    svgTitle(point, pointTooltip);
    attachChartTooltip(container, point, pointTooltip);
    svg.appendChild(point);
    const axisLabel = svgEl("text", { x: xFor(i), y: height - padding.bottom + 14, "text-anchor": "middle", class: "chart-axis-label" });
    axisLabel.textContent = "GW" + h.gameweek;
    svg.appendChild(axisLabel);
  });

  const topLabel = svgEl("text", { x: 4, y: padding.top + 4, class: "chart-axis-label" });
  topLabel.textContent = minRank.toLocaleString() + " (best)";
  svg.appendChild(topLabel);

  const bottomLabel = svgEl("text", { x: 4, y: height - padding.bottom, class: "chart-axis-label" });
  bottomLabel.textContent = maxRank.toLocaleString();
  svg.appendChild(bottomLabel);

  container.appendChild(svg);
}

// Picks a "nice" gridline step (1/2/5 × a power of 10) for a given max value
// and a rough target tick count — e.g. max=260, target=4 → step=100, so
// gridlines land on 0/100/200/300 instead of some awkward fraction.
function niceAxisStep(maxValue, targetTicks) {
  const rawStep = maxValue / Math.max(targetTicks, 1);
  if (rawStep <= 0) return 1;
  const magnitude = Math.pow(10, Math.floor(Math.log10(rawStep)));
  const residual = rawStep / magnitude;
  let niceResidual;
  if (residual > 5) niceResidual = 10;
  else if (residual > 2) niceResidual = 5;
  else if (residual > 1) niceResidual = 2;
  else niceResidual = 1;
  return niceResidual * magnitude;
}

function renderLeagueHistoryChart(container, legend, series) {
  container.innerHTML = "";
  legend.innerHTML = "";

  const withHistory = (series || []).filter((s) => s.history && s.history.length > 0);
  if (withHistory.length === 0) {
    container.appendChild(el("p", "empty-note", "No gameweek history available for this league yet."));
    return;
  }

  const allGameweeks = [...new Set(withHistory.flatMap((s) => s.history.map((h) => h.gameweek)))].sort((a, b) => a - b);
  const maxTotal = Math.max(...withHistory.flatMap((s) => s.history.map((h) => h.totalPoints)), 10);

  // Round the axis top up to a "nice" number (e.g. 300 rather than 267) and
  // build evenly-spaced gridlines from 0 up to it, so the y-axis reads like
  // 0-100-200-300 and adapts automatically to how high the totals actually go.
  const axisStep = niceAxisStep(maxTotal, 4);
  const axisMax = Math.ceil(maxTotal / axisStep) * axisStep;
  const axisTicks = [];
  for (let tick = 0; tick <= axisMax; tick += axisStep) axisTicks.push(tick);

  const width = Math.max(520, allGameweeks.length * 50 + 170);
  const height = 340;
  const padding = { top: 20, right: 160, bottom: 30, left: 40 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;

  const xFor = (gw) => padding.left + (allGameweeks.indexOf(gw) / Math.max(allGameweeks.length - 1, 1)) * chartWidth;
  const yFor = (pts) => padding.top + chartHeight - (axisMax > 0 ? (pts / axisMax) * chartHeight : 0);

  const svg = svgEl("svg", { viewBox: `0 0 ${width} ${height}`, class: "chart-svg league-chart-svg", role: "img", "aria-label": "Total points over the season, across the league" });

  axisTicks.forEach((tick) => {
    svg.appendChild(svgEl("line", {
      x1: padding.left, y1: yFor(tick), x2: width - padding.right, y2: yFor(tick),
      stroke: "#e4e8ee", "stroke-width": 1,
    }));
    const tickLabel = svgEl("text", { x: padding.left - 6, y: yFor(tick) + 3, "text-anchor": "end", class: "chart-axis-label" });
    tickLabel.textContent = tick.toLocaleString();
    svg.appendChild(tickLabel);
  });

  allGameweeks.forEach((gw) => {
    const axisLabel = svgEl("text", { x: xFor(gw), y: height - padding.bottom + 16, "text-anchor": "middle", class: "chart-axis-label" });
    axisLabel.textContent = "GW" + gw;
    svg.appendChild(axisLabel);
  });

  // Draw each manager's line and per-gameweek points first — hovering any
  // point shows its exact total via a native tooltip.
  const seriesMeta = withHistory.map((s, idx) => {
    const color = CHART_COLORS[idx % CHART_COLORS.length];
    const sorted = [...s.history].sort((a, b) => a.gameweek - b.gameweek);
    const linePoints = sorted.map((h) => `${xFor(h.gameweek)},${yFor(h.totalPoints)}`).join(" ");

    svg.appendChild(svgEl("polyline", { points: linePoints, fill: "none", stroke: color, "stroke-width": 2, opacity: 0.9 }));
    for (const h of sorted) {
      const point = svgEl("circle", { cx: xFor(h.gameweek), cy: yFor(h.totalPoints), r: 3, fill: color });
      const pointTooltip = `${s.managerName}: ${h.totalPoints} pts (GW${h.gameweek})`;
      svgTitle(point, pointTooltip);
      attachChartTooltip(container, point, pointTooltip);
      svg.appendChild(point);
    }

    const last = sorted[sorted.length - 1];
    return { s, color, last, rawY: yFor(last.totalPoints) };
  });

  // End-of-line name/crest labels tend to bunch up when several managers'
  // totals are close together (exactly what happened in the screenshot that
  // prompted this). Space them out vertically with a minimum gap, nudging
  // labels down (and, if that runs off the bottom, back up) from their
  // natural position — then draw a thin leader line back to the real point
  // whenever a label had to move.
  const minGap = 18;
  const minLabelY = padding.top + 8;
  const maxLabelY = height - padding.bottom - 8;
  const labelPositions = seriesMeta
    .map((m) => ({ ...m, y: m.rawY }))
    .sort((a, b) => a.y - b.y);

  for (let i = 1; i < labelPositions.length; i++) {
    if (labelPositions[i].y < labelPositions[i - 1].y + minGap) {
      labelPositions[i].y = labelPositions[i - 1].y + minGap;
    }
  }
  if (labelPositions.length > 0 && labelPositions[labelPositions.length - 1].y > maxLabelY) {
    labelPositions[labelPositions.length - 1].y = maxLabelY;
    for (let i = labelPositions.length - 2; i >= 0; i--) {
      if (labelPositions[i].y > labelPositions[i + 1].y - minGap) {
        labelPositions[i].y = labelPositions[i + 1].y - minGap;
      }
    }
  }
  if (labelPositions.length > 0 && labelPositions[0].y < minLabelY) {
    labelPositions[0].y = minLabelY;
    for (let i = 1; i < labelPositions.length; i++) {
      if (labelPositions[i].y < labelPositions[i - 1].y + minGap) {
        labelPositions[i].y = labelPositions[i - 1].y + minGap;
      }
    }
  }

  labelPositions.forEach(({ s, color, last, rawY, y }) => {
    const labelX = xFor(last.gameweek) + 8;
    const tooltipText = `${s.managerName}: ${last.totalPoints} pts`;

    // Only draw a leader line when the label actually had to move — keeps
    // the chart clean when there's no crowding to resolve.
    if (Math.abs(y - rawY) > 2) {
      svg.appendChild(svgEl("line", {
        x1: xFor(last.gameweek), y1: rawY, x2: labelX - 2, y2: y,
        stroke: color, "stroke-width": 1, opacity: 0.35,
      }));
    }

    let textX = labelX;

    if (s.crestUrl) {
      const crest = svgEl("image", { x: labelX, y: y - 8, width: 16, height: 16 });
      crest.setAttributeNS("http://www.w3.org/1999/xlink", "href", s.crestUrl);
      crest.setAttribute("href", s.crestUrl);
      crest.addEventListener("error", () => crest.remove());
      svgTitle(crest, tooltipText);
      attachChartTooltip(container, crest, tooltipText);
      svg.appendChild(crest);
      textX = labelX + 20;
    }

    const nameLabel = svgEl("text", { x: textX, y: y + 4, class: "chart-series-label", fill: color });
    nameLabel.textContent = s.managerName;
    svgTitle(nameLabel, tooltipText);
    attachChartTooltip(container, nameLabel, tooltipText);
    svg.appendChild(nameLabel);
  });

  container.appendChild(svg);

  withHistory.forEach((s, idx) => {
    const color = CHART_COLORS[idx % CHART_COLORS.length];
    const item = el("div", "legend-item");
    const swatch = el("span", "legend-swatch");
    swatch.style.background = color;
    item.appendChild(swatch);
    item.appendChild(el("span", null, `${s.managerName} (${s.teamName})`));
    legend.appendChild(item);
  });
}

loadTeamBtn.addEventListener("click", loadTeam);
refreshDataBtn.addEventListener("click", refreshPlayerData);
teamIdInput.addEventListener("keydown", (e) => {
  if (e.key === "Enter") loadTeam();
});
loadLeagueBtn.addEventListener("click", loadLeague);
leagueIdInput.addEventListener("keydown", (e) => {
  if (e.key === "Enter") loadLeague();
});
