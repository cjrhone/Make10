import { useState, useCallback, useRef } from "react";

const S = 5;
const clone = (g) => g.map((r) => [...r]);
const isLocked = (v) => v >= 10;

/* ─── slower difficulty curve, max 9 ─── */
const getDiff = (mc) => {
  if (mc < 8) return { min: 0, max: 4, label: "calm" };
  if (mc < 18) return { min: 0, max: 5, label: "gentle" };
  if (mc < 30) return { min: 0, max: 6, label: "steady" };
  if (mc < 45) return { min: 0, max: 7, label: "rising" };
  if (mc < 65) return { min: 0, max: 8, label: "focused" };
  if (mc < 90) return { min: 0, max: 9, label: "deep" };
  return { min: 1, max: 9, label: "mastery" };
};

const rand = (min, max) => min + Math.floor(Math.random() * (max - min + 1));

/* ─── match: row/col summing to multiple of 10 ─── */
const findMatch = (grid) => {
  for (let r = 0; r < S; r++) {
    const sum = grid[r].reduce((a, b) => a + b, 0);
    if (sum > 0 && sum % 10 === 0) return { type: "row", idx: r, sum };
  }
  for (let c = 0; c < S; c++) {
    let sum = 0;
    for (let r = 0; r < S; r++) sum += grid[r][c];
    if (sum > 0 && sum % 10 === 0) return { type: "col", idx: c, sum };
  }
  return null;
};

/* ─── gravity: ALL tiles fall, including locked. New randoms fill from top ─── */
const applyGravity = (grid, removed, diff) => {
  const g = clone(grid);
  for (let c = 0; c < S; c++) {
    const col = [];
    for (let r = 0; r < S; r++)
      if (!removed.has(`${r}-${c}`)) col.push(g[r][c]);
    const fill = Array.from({ length: S - col.length }, () => rand(diff.min, diff.max));
    const full = [...fill, ...col];
    for (let r = 0; r < S; r++) g[r][c] = full[r];
  }
  return g;
};

/* ─── resolve match: merge at position, remove others, gravity ─── */
const resolveMatch = (grid, match, mergePos, diff) => {
  const g = clone(grid);
  const removed = new Set();

  if (match.type === "row") {
    g[match.idx][mergePos] = match.sum;
    for (let c = 0; c < S; c++)
      if (c !== mergePos) removed.add(`${match.idx}-${c}`);
  } else {
    g[mergePos][match.idx] = match.sum;
    for (let r = 0; r < S; r++)
      if (r !== mergePos) removed.add(`${r}-${match.idx}`);
  }
  return applyGravity(g, removed, diff);
};

/* ─── merge pos: check both swap positions on the matching line ─── */
const getMergePos = (match, firstTap, secondTap) => {
  // prefer secondTap (where the swapped tile landed)
  if (match.type === "row") {
    if (secondTap && secondTap.r === match.idx) return secondTap.c;
    if (firstTap && firstTap.r === match.idx) return firstTap.c;
  } else {
    if (secondTap && secondTap.c === match.idx) return secondTap.r;
    if (firstTap && firstTap.c === match.idx) return firstTap.r;
  }
  return 2;
};

/* ─── check for valid moves ─── */
const getFreeCells = (grid) => {
  const cells = [];
  for (let r = 0; r < S; r++)
    for (let c = 0; c < S; c++)
      if (!isLocked(grid[r][c])) cells.push({ r, c });
  return cells;
};

const hasMove = (grid) => {
  const free = getFreeCells(grid);
  for (let i = 0; i < free.length; i++)
    for (let j = i + 1; j < free.length; j++) {
      const g = clone(grid);
      const a = free[i], b = free[j];
      [g[a.r][a.c], g[b.r][b.c]] = [g[b.r][b.c], g[a.r][a.c]];
      if (findMatch(g)) return true;
    }
  return false;
};

const resetBoard = (grid, diff) => {
  for (let i = 0; i < 500; i++) {
    const g = clone(grid);
    for (let r = 0; r < S; r++)
      for (let c = 0; c < S; c++)
        if (!isLocked(g[r][c])) g[r][c] = rand(diff.min, diff.max);
    if (!findMatch(g) && hasMove(g)) return g;
  }
  const g = clone(grid);
  for (let r = 0; r < S; r++)
    for (let c = 0; c < S; c++)
      if (!isLocked(g[r][c])) g[r][c] = rand(diff.min, diff.max);
  return g;
};

const makeCleanGrid = (diff) => {
  for (let i = 0; i < 500; i++) {
    const g = Array.from({ length: S }, () =>
      Array.from({ length: S }, () => rand(diff.min, diff.max))
    );
    if (!findMatch(g) && hasMove(g)) return g;
  }
  return Array.from({ length: S }, () =>
    Array.from({ length: S }, () => rand(diff.min, diff.max))
  );
};

/* ─── tile palette ─── */
const tileColor = (v) => {
  if (v === 0) return { bg: "rgba(200,210,220,0.06)", fg: "rgba(180,190,200,0.3)", border: "rgba(180,190,200,0.08)", glow: null };
  if (v === 1) return { bg: "#1a2d4a", fg: "#6aa8d0", border: "#253f68", glow: null };
  if (v === 2) return { bg: "#182e45", fg: "#5e9ec4", border: "#224870", glow: null };
  if (v === 3) return { bg: "#153648", fg: "#52b4a8", border: "#1c5060", glow: null };
  if (v === 4) return { bg: "#173838", fg: "#44c4a0", border: "#205550", glow: null };
  if (v === 5) return { bg: "#254225", fg: "#6ed080", border: "#345e34", glow: null };
  if (v === 6) return { bg: "#42381c", fg: "#c8b050", border: "#5e5028", glow: null };
  if (v === 7) return { bg: "#422a1c", fg: "#d48e50", border: "#5e4028", glow: null };
  if (v === 8) return { bg: "#421c24", fg: "#d46464", border: "#5e2c38", glow: null };
  if (v === 9) return { bg: "#3e1838", fg: "#c858a8", border: "#582848", glow: null };
  // locked tiers with glow colors
  if (v === 10) return { bg: "linear-gradient(135deg,#342a14,#504018,#342a14)", fg: "#f0d060", border: "#806828", glow: "240,208,96" };
  if (v === 20) return { bg: "linear-gradient(135deg,#2a1a30,#4a2860,#2a1a30)", fg: "#d090ff", border: "#6a3890", glow: "208,144,255" };
  if (v === 30) return { bg: "linear-gradient(135deg,#0a2a2a,#105050,#0a2a2a)", fg: "#50e8d0", border: "#208870", glow: "80,232,208" };
  if (v === 40) return { bg: "linear-gradient(135deg,#2a0a0a,#601818,#2a0a0a)", fg: "#ff6060", border: "#903030", glow: "255,96,96" };
  if (v === 50) return { bg: "linear-gradient(135deg,#2a200a,#605018,#2a200a)", fg: "#ffa030", border: "#906020", glow: "255,160,48" };
  if (v === 60) return { bg: "linear-gradient(135deg,#0a1a2a,#183060,#0a1a2a)", fg: "#60a0ff", border: "#305090", glow: "96,160,255" };
  if (v >= 70) return { bg: "linear-gradient(135deg,#1a0a2a,#401860,#1a0a2a)", fg: "#e060e0", border: "#803080", glow: "224,96,224" };
  return { bg: "#222", fg: "#aaa", border: "#444", glow: null };
};

const Heart = ({ filled, breaking }) => (
  <span
    style={{
      fontSize: 18,
      color: filled ? "#e05050" : "rgba(255,255,255,0.1)",
      transition: "all 0.3s ease",
      transform: breaking ? "scale(1.4)" : "scale(1)",
      filter: breaking ? "brightness(1.5)" : "none",
      display: "inline-block",
    }}
  >
    {filled ? "♥" : "♡"}
  </span>
);

/* ─────────────────────────────────────── */
export default function Make10Zen() {
  const initD = getDiff(0);
  const [grid, setGrid] = useState(() => makeCleanGrid(initD));
  const [sel, setSel] = useState(null);
  const [lives, setLives] = useState(3);
  const [over, setOver] = useState(false);
  const [moves, setMoves] = useState(0);
  const [matchCount, setMatchCount] = useState(0);
  const mcRef = useRef(0);
  const [chains, setChains] = useState(0);
  const chainsRef = useRef(0);
  const [resets, setResets] = useState(0);

  const [hlCells, setHlCells] = useState(new Set());
  const [mergeCells, setMergeCells] = useState(new Set());
  const [wrongCells, setWrongCells] = useState(new Set());
  const [shaking, setShaking] = useState(false);
  const [breakingHeart, setBreakingHeart] = useState(false);
  const [showReshuffle, setShowReshuffle] = useState(false);
  const [showShift, setShowShift] = useState(false);
  const [diffLabel, setDiffLabel] = useState(initD.label);
  const [swapAnim, setSwapAnim] = useState(null);
  const busyRef = useRef(false);

  const checkNoMoves = useCallback((g) => {
    const free = getFreeCells(g);
    if (free.length < 2 || !hasMove(g)) {
      setShowReshuffle(true);
      setTimeout(() => {
        const d = getDiff(mcRef.current);
        const newG = resetBoard(g, d);
        setGrid(newG);
        setResets((r) => r + 1);
        setShowReshuffle(false);
        busyRef.current = false;
        if (getFreeCells(newG).length < 2 || !hasMove(newG)) setOver(true);
      }, 900);
      return true;
    }
    return false;
  }, []);

  /* ─── resolve chain: pass both taps for merge positioning ─── */
  const resolveChain = useCallback(
    (g, firstTap, secondTap, depth = 0) => {
      const match = findMatch(g);
      if (!match) {
        if (!checkNoMoves(g)) busyRef.current = false;
        return;
      }

      mcRef.current += 1;
      setMatchCount(mcRef.current);

      if (depth > 0) {
        chainsRef.current += 1;
        setChains(chainsRef.current);
      }

      const prev = getDiff(mcRef.current - 1);
      const next = getDiff(mcRef.current);
      if (next.label !== prev.label) {
        setDiffLabel(next.label);
        setShowShift(true);
        setTimeout(() => setShowShift(false), 1800);
      }

      const mergePos = getMergePos(match, firstTap, secondTap);
      const cells = new Set();
      const mCells = new Set();

      if (match.type === "row") {
        for (let c = 0; c < S; c++) {
          cells.add(`${match.idx}-${c}`);
          if (c === mergePos) mCells.add(`${match.idx}-${c}`);
        }
      } else {
        for (let r = 0; r < S; r++) {
          cells.add(`${r}-${match.idx}`);
          if (r === mergePos) mCells.add(`${r}-${match.idx}`);
        }
      }

      setHlCells(cells);
      setMergeCells(mCells);

      setTimeout(() => {
        const d = getDiff(mcRef.current);
        const newG = resolveMatch(g, match, mergePos, d);
        setHlCells(new Set());
        setMergeCells(new Set());
        setGrid(newG);
        setTimeout(() => resolveChain(newG, null, null, depth + 1), 280);
      }, 520);
    },
    [checkNoMoves]
  );

  const doWrongMove = useCallback((g, a, b, newLives) => {
    setWrongCells(new Set([`${a.r}-${a.c}`, `${b.r}-${b.c}`]));
    setShaking(true);
    setBreakingHeart(true);
    setTimeout(() => {
      const reverted = clone(g);
      [reverted[a.r][a.c], reverted[b.r][b.c]] = [
        reverted[b.r][b.c],
        reverted[a.r][a.c],
      ];
      setGrid(reverted);
      setWrongCells(new Set());
      setShaking(false);
      setBreakingHeart(false);
      if (newLives <= 0) setTimeout(() => setOver(true), 200);
      busyRef.current = false;
    }, 500);
  }, []);

  const handleTap = useCallback(
    (r, c) => {
      if (over || busyRef.current) return;
      if (isLocked(grid[r][c])) return;
      if (!sel) {
        setSel({ r, c });
        return;
      }
      if (sel.r === r && sel.c === c) {
        setSel(null);
        return;
      }
      if (isLocked(grid[r][c])) {
        setSel(null);
        return;
      }

      const firstTap = sel;
      const secondTap = { r, c };
      setSel(null);
      setMoves((m) => m + 1);
      busyRef.current = true;

      // swap: firstTap value goes to secondTap position and vice versa
      const g = clone(grid);
      [g[firstTap.r][firstTap.c], g[secondTap.r][secondTap.c]] = [
        g[secondTap.r][secondTap.c],
        g[firstTap.r][firstTap.c],
      ];

      setSwapAnim({ a: firstTap, b: secondTap, phase: "out" });

      setTimeout(() => {
        setGrid(g);
        setSwapAnim({ a: firstTap, b: secondTap, phase: "in" });
        setTimeout(() => {
          setSwapAnim(null);
          const match = findMatch(g);
          if (match) {
            resolveChain(g, firstTap, secondTap, 0);
          } else {
            const newLives = lives - 1;
            setLives(newLives);
            doWrongMove(g, firstTap, secondTap, newLives);
          }
        }, 150);
      }, 150);
    },
    [grid, sel, over, lives, resolveChain, doWrongMove]
  );

  const restart = () => {
    const d = getDiff(0);
    setGrid(makeCleanGrid(d));
    setSel(null);
    setLives(3);
    setOver(false);
    setMoves(0);
    setMatchCount(0);
    mcRef.current = 0;
    setChains(0);
    chainsRef.current = 0;
    setResets(0);
    setHlCells(new Set());
    setMergeCells(new Set());
    setWrongCells(new Set());
    setShaking(false);
    setBreakingHeart(false);
    setShowReshuffle(false);
    setShowShift(false);
    setDiffLabel(d.label);
    setSwapAnim(null);
    busyRef.current = false;
  };

  let lockedCount = 0,
    highestOnBoard = 0;
  for (let r = 0; r < S; r++)
    for (let c = 0; c < S; c++)
      if (isLocked(grid[r][c])) {
        lockedCount++;
        if (grid[r][c] > highestOnBoard) highestOnBoard = grid[r][c];
      }
  const diff = getDiff(matchCount);

  /* ─── generate unique keyframe names for each glow color ─── */
  const glowKeyframes = `
    @keyframes shake {
      0%,100%{transform:translateX(0)}
      15%{transform:translateX(-5px) rotate(-0.5deg)}
      30%{transform:translateX(4px) rotate(0.4deg)}
      45%{transform:translateX(-3px) rotate(-0.3deg)}
      60%{transform:translateX(3px) rotate(0.2deg)}
      75%{transform:translateX(-2px)}
      90%{transform:translateX(1px)}
    }
    @keyframes glow10 {
      0%,100%{box-shadow:0 0 6px rgba(240,208,96,0.15),inset 0 0 4px rgba(240,208,96,0.05),0 0 0 1px rgba(240,208,96,0.2)}
      50%{box-shadow:0 0 14px rgba(240,208,96,0.3),inset 0 0 8px rgba(240,208,96,0.1),0 0 0 1.5px rgba(240,208,96,0.35)}
    }
    @keyframes glow20 {
      0%,100%{box-shadow:0 0 6px rgba(208,144,255,0.15),inset 0 0 4px rgba(208,144,255,0.05),0 0 0 1px rgba(208,144,255,0.2)}
      50%{box-shadow:0 0 14px rgba(208,144,255,0.3),inset 0 0 8px rgba(208,144,255,0.1),0 0 0 1.5px rgba(208,144,255,0.35)}
    }
    @keyframes glow30 {
      0%,100%{box-shadow:0 0 6px rgba(80,232,208,0.15),inset 0 0 4px rgba(80,232,208,0.05),0 0 0 1px rgba(80,232,208,0.2)}
      50%{box-shadow:0 0 14px rgba(80,232,208,0.3),inset 0 0 8px rgba(80,232,208,0.1),0 0 0 1.5px rgba(80,232,208,0.35)}
    }
    @keyframes glow40 {
      0%,100%{box-shadow:0 0 6px rgba(255,96,96,0.15),inset 0 0 4px rgba(255,96,96,0.05),0 0 0 1px rgba(255,96,96,0.2)}
      50%{box-shadow:0 0 14px rgba(255,96,96,0.3),inset 0 0 8px rgba(255,96,96,0.1),0 0 0 1.5px rgba(255,96,96,0.35)}
    }
    @keyframes glow50 {
      0%,100%{box-shadow:0 0 6px rgba(255,160,48,0.15),inset 0 0 4px rgba(255,160,48,0.05),0 0 0 1px rgba(255,160,48,0.2)}
      50%{box-shadow:0 0 14px rgba(255,160,48,0.3),inset 0 0 8px rgba(255,160,48,0.1),0 0 0 1.5px rgba(255,160,48,0.35)}
    }
    @keyframes glow60 {
      0%,100%{box-shadow:0 0 6px rgba(96,160,255,0.15),inset 0 0 4px rgba(96,160,255,0.05),0 0 0 1px rgba(96,160,255,0.2)}
      50%{box-shadow:0 0 14px rgba(96,160,255,0.3),inset 0 0 8px rgba(96,160,255,0.1),0 0 0 1.5px rgba(96,160,255,0.35)}
    }
    @keyframes glow70 {
      0%,100%{box-shadow:0 0 6px rgba(224,96,224,0.15),inset 0 0 4px rgba(224,96,224,0.05),0 0 0 1px rgba(224,96,224,0.2)}
      50%{box-shadow:0 0 14px rgba(224,96,224,0.3),inset 0 0 8px rgba(224,96,224,0.1),0 0 0 1.5px rgba(224,96,224,0.35)}
    }
  `;

  const getGlowAnim = (v) => {
    if (v === 10) return "glow10 2.5s ease-in-out infinite";
    if (v === 20) return "glow20 2.5s ease-in-out infinite";
    if (v === 30) return "glow30 2.5s ease-in-out infinite";
    if (v === 40) return "glow40 2.5s ease-in-out infinite";
    if (v === 50) return "glow50 2.5s ease-in-out infinite";
    if (v === 60) return "glow60 2.5s ease-in-out infinite";
    if (v >= 70) return "glow70 2.5s ease-in-out infinite";
    return "none";
  };

  return (
    <div
      style={{
        minHeight: "100vh",
        width: "100%",
        background:
          "linear-gradient(170deg, #0a0f1a 0%, #0f1628 40%, #111a2e 100%)",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        fontFamily: "'Georgia','Garamond',serif",
        color: "#c8d0dc",
        padding: "16px 0",
        boxSizing: "border-box",
        userSelect: "none",
        WebkitUserSelect: "none",
        WebkitTapHighlightColor: "transparent",
        touchAction: "manipulation",
        overflow: "hidden",
      }}
    >
      <style>{glowKeyframes}</style>

      {/* toasts */}
      <div
        style={{
          position: "fixed",
          top: 36,
          left: "50%",
          transform: `translateX(-50%) translateY(${showShift ? 0 : -60}px)`,
          opacity: showShift ? 1 : 0,
          transition: "all 0.6s cubic-bezier(0.16,1,0.3,1)",
          background: "rgba(240,208,96,0.08)",
          border: "1px solid rgba(240,208,96,0.2)",
          borderRadius: 20,
          padding: "8px 20px",
          fontSize: 11,
          letterSpacing: "0.3em",
          color: "#d4a050",
          textTransform: "uppercase",
          zIndex: 50,
          backdropFilter: "blur(8px)",
          pointerEvents: "none",
        }}
      >
        {diffLabel}
      </div>

      <div
        style={{
          position: "fixed",
          top: 36,
          left: "50%",
          transform: `translateX(-50%) translateY(${showReshuffle ? 0 : -60}px)`,
          opacity: showReshuffle ? 1 : 0,
          transition: "all 0.5s cubic-bezier(0.16,1,0.3,1)",
          background: "rgba(100,140,200,0.1)",
          border: "1px solid rgba(100,140,200,0.25)",
          borderRadius: 20,
          padding: "8px 20px",
          fontSize: 11,
          letterSpacing: "0.3em",
          color: "#7a9ac0",
          textTransform: "uppercase",
          zIndex: 50,
          backdropFilter: "blur(8px)",
          pointerEvents: "none",
        }}
      >
        reshuffling
      </div>

      {/* title */}
      <div style={{ textAlign: "center", marginBottom: 14 }}>
        <h1
          style={{
            fontSize: 28,
            fontWeight: 400,
            letterSpacing: "0.18em",
            margin: 0,
            color: "#8a9ab0",
            textTransform: "uppercase",
          }}
        >
          make
          <span style={{ color: "#f0d060", fontWeight: 600 }}>10</span>
        </h1>
        <div
          style={{
            fontSize: 11,
            letterSpacing: "0.35em",
            color: "#4a5568",
            marginTop: 4,
            textTransform: "uppercase",
          }}
        >
          zen
        </div>
      </div>

      {/* lives */}
      <div style={{ display: "flex", gap: 6, marginBottom: 10 }}>
        {[0, 1, 2].map((i) => (
          <Heart
            key={i}
            filled={i < lives}
            breaking={breakingHeart && i === lives}
          />
        ))}
      </div>

      {/* stats */}
      <div
        style={{
          display: "flex",
          gap: 16,
          marginBottom: 6,
          fontSize: 11,
          letterSpacing: "0.08em",
          color: "#5a6a80",
          flexWrap: "wrap",
          justifyContent: "center",
        }}
      >
        <div>
          moves{" "}
          <span style={{ color: "#8a9ab0", fontWeight: 600 }}>{moves}</span>
        </div>
        <div>
          tens{" "}
          <span style={{ color: "#d4a050", fontWeight: 600 }}>
            {matchCount}
          </span>
        </div>
        <div>
          chains{" "}
          <span style={{ color: "#7a8898", fontWeight: 600 }}>{chains}</span>
        </div>
        {highestOnBoard > 0 && (
          <div>
            best{" "}
            <span
              style={{
                color: tileColor(highestOnBoard).fg,
                fontWeight: 600,
              }}
            >
              {highestOnBoard}
            </span>
          </div>
        )}
      </div>

      {/* board pressure + tile range */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          marginBottom: 14,
          fontSize: 10,
          color: "#3a4a5a",
          letterSpacing: "0.1em",
        }}
      >
        {lockedCount > 0 && (
          <>
            <span style={{ color: "#f0d060", fontSize: 11 }}>◆</span>
            <span
              style={{
                color:
                  lockedCount > 16
                    ? "#e05050"
                    : lockedCount > 10
                    ? "#d4a050"
                    : "#5a6a80",
                fontWeight: 600,
                fontSize: 12,
                transition: "color 0.3s",
              }}
            >
              {lockedCount}
            </span>
            <span style={{ color: "rgba(255,255,255,0.06)" }}>·</span>
          </>
        )}
        <div style={{ display: "flex", gap: 3, alignItems: "center" }}>
          {Array.from(
            { length: diff.max - diff.min + 1 },
            (_, i) => diff.min + i
          ).map((n) => (
            <div
              key={n}
              style={{
                width: 16,
                height: 16,
                borderRadius: 3,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: 8,
                fontWeight: 600,
                color: tileColor(n).fg,
                background: tileColor(n).bg,
                border: `1px solid ${tileColor(n).border}`,
                opacity: 0.6,
              }}
            >
              {n}
            </div>
          ))}
        </div>
      </div>

      {/* grid */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: `repeat(${S},1fr)`,
          gap: 6,
          width: "min(82vw, 360px)",
          aspectRatio: "1",
          padding: 10,
          background: "rgba(255,255,255,0.02)",
          borderRadius: 16,
          border: "1px solid rgba(255,255,255,0.04)",
          animation: shaking ? "shake 0.4s ease-in-out" : "none",
        }}
      >
        {grid.map((row, r) =>
          row.map((val, c) => {
            const key = `${r}-${c}`;
            const locked = isLocked(val);
            const isSel = sel && sel.r === r && sel.c === c;
            const isHL = hlCells.has(key);
            const isMerge = mergeCells.has(key);
            const isWrong = wrongCells.has(key);
            const tc = tileColor(val);

            const isSwapTile =
              swapAnim &&
              ((swapAnim.a.r === r && swapAnim.a.c === c) ||
                (swapAnim.b.r === r && swapAnim.b.c === c));
            const swapScale = isSwapTile
              ? swapAnim.phase === "out"
                ? 0.65
                : 1
              : 1;

            let bg = tc.bg,
              fg = tc.fg;
            let border = `1px solid ${tc.border || "transparent"}`;
            let shadow = "inset 0 1px 1px rgba(255,255,255,0.04)";
            let transform = `scale(${swapScale})`;
            let opacity = 1;
            let anim = locked ? getGlowAnim(val) : "none";

            if (isWrong) {
              border = "2px solid rgba(224,80,80,0.8)";
              shadow = "0 0 16px rgba(224,80,80,0.3)";
              bg = "rgba(224,80,80,0.15)";
              anim = "none";
            } else if (isHL) {
              anim = "none";
              if (isMerge) {
                bg = "radial-gradient(circle, #f0d060 0%, #a08030 100%)";
                fg = "#fff";
                shadow = "0 0 24px rgba(240,208,96,0.4)";
                transform = "scale(1.04)";
              } else {
                bg = "rgba(240,208,96,0.2)";
                fg = "rgba(255,255,255,0.6)";
                shadow = "0 0 20px rgba(240,208,96,0.3)";
                transform = "scale(0.88)";
                opacity = 0.6;
              }
            } else if (isSel) {
              border = "2px solid rgba(240,208,96,0.8)";
              shadow =
                "0 0 16px rgba(240,208,96,0.25), inset 0 0 12px rgba(240,208,96,0.1)";
              transform = "scale(1.08)";
              anim = "none";
            } else if (sel && !locked) {
              border = "1px solid rgba(240,208,96,0.12)";
            }

            return (
              <div
                key={key}
                onClick={() => handleTap(r, c)}
                style={{
                  aspectRatio: "1",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  borderRadius: locked ? 12 : 10,
                  cursor: locked ? "default" : "pointer",
                  fontSize: locked
                    ? "clamp(11px,3vw,15px)"
                    : val === 0
                    ? "clamp(8px,2vw,10px)"
                    : "clamp(16px,4.5vw,24px)",
                  fontWeight: locked ? 700 : 500,
                  fontFamily: "'Georgia',serif",
                  color: fg,
                  background: bg,
                  border,
                  boxShadow: shadow,
                  transition: "all 0.18s ease",
                  transform,
                  opacity,
                  animation: anim,
                  position: "relative",
                }}
              >
                {val === 0 ? (
                  <span style={{ opacity: 0.35 }}>·</span>
                ) : locked ? (
                  <span>{val}</span>
                ) : (
                  val
                )}
                {locked && (
                  <div
                    style={{
                      position: "absolute",
                      bottom: 2,
                      right: 3,
                      fontSize: 5,
                      color: tc.fg,
                      opacity: 0.35,
                    }}
                  >
                    ◆
                  </div>
                )}
              </div>
            );
          })
        )}
      </div>

      {/* hint */}
      <div
        style={{
          marginTop: 18,
          fontSize: 11,
          color: "#3a4a5a",
          letterSpacing: "0.08em",
          textAlign: "center",
          lineHeight: 1.7,
          maxWidth: 280,
        }}
      >
        {over
          ? ""
          : sel
          ? "tap any free tile to swap — wrong moves cost ♥"
          : lockedCount === 0
          ? "tap to select · swap to make rows or columns sum to 10"
          : "◆ tiles are locked — keep building"}
      </div>

      {/* game over */}
      {over && (
        <div
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(8,12,24,0.9)",
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 100,
            backdropFilter: "blur(12px)",
          }}
        >
          <div
            style={{
              fontSize: 11,
              letterSpacing: "0.4em",
              color: "#5a6a80",
              textTransform: "uppercase",
              marginBottom: 12,
            }}
          >
            {lives <= 0 ? "no more lives" : "board full"}
          </div>
          <div
            style={{
              fontSize: 34,
              fontWeight: 400,
              color: "#8a9ab0",
              letterSpacing: "0.15em",
              marginBottom: 8,
            }}
          >
            stillness
          </div>
          {highestOnBoard > 0 && (
            <div
              style={{
                fontSize: 14,
                color: tileColor(highestOnBoard).fg,
                marginBottom: 20,
                letterSpacing: "0.15em",
              }}
            >
              highest tile · {highestOnBoard}
            </div>
          )}
          <div
            style={{
              display: "flex",
              gap: 6,
              marginBottom: 32,
              flexWrap: "wrap",
              justifyContent: "center",
            }}
          >
            {[
              { l: "moves", v: moves, c: "#8a9ab0" },
              { l: "tens", v: matchCount, c: "#d4a050" },
              { l: "chains", v: chains, c: "#7a8898" },
              { l: "reshuffles", v: resets, c: "#7a9ac0" },
            ].map((s) => (
              <div
                key={s.l}
                style={{
                  background: "rgba(255,255,255,0.03)",
                  borderRadius: 10,
                  padding: "10px 14px",
                  textAlign: "center",
                  border: "1px solid rgba(255,255,255,0.05)",
                  minWidth: 60,
                }}
              >
                <div style={{ fontSize: 20, fontWeight: 600, color: s.c }}>
                  {s.v}
                </div>
                <div
                  style={{
                    fontSize: 8,
                    letterSpacing: "0.12em",
                    color: "#4a5a6a",
                    marginTop: 2,
                    textTransform: "uppercase",
                  }}
                >
                  {s.l}
                </div>
              </div>
            ))}
          </div>
          <button
            onClick={restart}
            style={{
              background: "none",
              border: "1px solid rgba(240,208,96,0.3)",
              color: "#d4a050",
              padding: "12px 36px",
              borderRadius: 8,
              fontSize: 13,
              letterSpacing: "0.2em",
              cursor: "pointer",
              fontFamily: "'Georgia',serif",
              textTransform: "uppercase",
              transition: "all 0.2s ease",
            }}
            onMouseEnter={(e) => {
              e.target.style.borderColor = "rgba(240,208,96,0.6)";
              e.target.style.boxShadow = "0 0 20px rgba(240,208,96,0.15)";
            }}
            onMouseLeave={(e) => {
              e.target.style.borderColor = "rgba(240,208,96,0.3)";
              e.target.style.boxShadow = "none";
            }}
          >
            begin again
          </button>
        </div>
      )}
    </div>
  );
}
