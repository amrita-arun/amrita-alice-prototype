#!/usr/bin/env python3
"""Solver for LOT levels. Mirrors the rules in Assets/Scripts/Lot/LotRules.cs.

Usage: python3 Tools/solve_levels.py [path/to/Levels.txt]

For each level prints the board, the minimum number of drags to win under the
real rules, and the minimum under "naive" rules (no lies) so you can check that
each lie actually matters, plus how many reachable states are dead ends (fuel traps).
"""
import sys
from collections import deque
from pathlib import Path

DEFAULT = Path(__file__).resolve().parent.parent / "AmritaAlicePrototype/Assets/Resources/Levels.txt"
N = 6
DIRS = {"left": (-1, 0), "right": (1, 0), "up": (0, 1), "down": (0, -1)}


class Car:
    def __init__(self, x, y, length, horiz, color, flags):
        self.x, self.y, self.len, self.h, self.color = x, y, length, horiz, color
        self.target = "target" in flags
        self.light = next((f for f in flags if f in DIRS), None)
        self.fuel = next((int(f[4:]) for f in flags if f.startswith("fuel")), -1)  # -1 = unlimited


def parse(text):
    levels, cur = [], None
    for raw in text.splitlines():
        line = raw.split("#")[0].strip()
        if not line:
            continue
        t = line.split()
        if t[0] == "level":
            cur = {"name": " ".join(t[1:]), "cars": [], "exit": None, "notch": None}
        elif t[0] in ("exit", "notch"):
            cur[t[0]] = (t[1], int(t[2]))
        elif t[0] == "car":
            cur["cars"].append(Car(int(t[1]), int(t[2]), int(t[3]), t[4] == "H", t[5], t[6:]))
        elif t[0] == "end":
            levels.append(cur)
    return levels


def cells(c, x, y):
    return [(x + i, y) if c.h else (x, y + i) for i in range(c.len)]


def solve(lv, naive=False, start=None, count_dead=False):
    """Min drags to win. State = (positions, fuel). naive = no lies (no links, lights, fuel; red is hero)."""
    cars = lv["cars"]
    groups = {}
    for i, c in enumerate(cars):
        key = c.color if not naive and not c.target else f"_{i}"
        groups.setdefault(key, []).append(i)
    tgt = next(i for i, c in enumerate(cars) if c.target)
    if naive:  # naive player: red car is the hero and the notch is the exit
        tgt = next((i for i, c in enumerate(cars) if c.color == "red"), tgt)
    side, idx = (lv["notch"] or lv["exit"]) if naive else lv["exit"]

    def occ(pos, skip):
        o = set()
        for i in range(len(cars)):
            if i not in skip:
                o.update(cells(cars[i], *pos[i]))
        return o

    def has_fuel(fuel, members):
        return naive or all(fuel[m] != 0 for m in members)

    def can_exit(pos, fuel):
        c = cars[tgt]
        if not has_fuel(fuel, [tgt]):
            return False
        x, y = pos[tgt]
        if c.h and side in ("L", "R") and y == idx:
            d = 1 if side == "R" else -1
        elif not c.h and side in ("T", "B") and x == idx:
            d = 1 if side == "T" else -1
        else:
            return False
        if not naive and c.light and DIRS[c.light] != ((d, 0) if c.h else (0, d)):
            return False
        o = occ(pos, {tgt})
        ax = (d, 0) if c.h else (0, d)
        cx, cy = x, y
        while True:
            cx, cy = cx + ax[0], cy + ax[1]
            if any(not (0 <= a < N and 0 <= b < N) for a, b in cells(c, cx, cy)):
                return True
            if any(p in o for p in cells(c, cx, cy)):
                return False

    def moves(state):
        pos, fuel = state
        for members in groups.values():
            if not has_fuel(fuel, members):
                continue
            c0 = cars[members[0]]
            o = occ(pos, set(members))
            for sign in (-1, 1):
                ax = (sign, 0) if c0.h else (0, sign)
                if not naive and any(cars[m].light and DIRS[cars[m].light] != ax for m in members):
                    continue
                k = 0
                while True:
                    k += 1
                    np_ = list(pos)
                    if any(any(not (0 <= a < N and 0 <= b < N) or (a, b) in o
                               for a, b in cells(cars[m], pos[m][0] + ax[0] * k, pos[m][1] + ax[1] * k)) for m in members):
                        break
                    nf = list(fuel)
                    for m in members:
                        np_[m] = (pos[m][0] + ax[0] * k, pos[m][1] + ax[1] * k)
                        if not naive and nf[m] > 0:
                            nf[m] -= 1
                    yield (tuple(np_), tuple(nf)), members[0], sign * k

    s0 = start or (tuple((c.x, c.y) for c in cars), tuple(c.fuel for c in cars))
    seen = {s0: None}
    preds = {s0: []}
    q = deque([s0])
    best = (1, []) if can_exit(*s0) else None
    while q and (best is None or count_dead):
        s = q.popleft()
        for ns, ci, dk in moves(s):
            if count_dead:
                preds.setdefault(ns, []).append(s)
            if ns in seen:
                continue
            seen[ns] = (s, ci, dk)
            if best is None and can_exit(*ns):
                path, cur = [], ns
                while seen[cur]:
                    prev, c, d = seen[cur]
                    path.append(f"{cars[c].color}{'+' if d > 0 else ''}{d}")
                    cur = prev
                best = (len(path) + 1, path[::-1])
            q.append(ns)
    best = best or (None, None)
    if not count_dead:
        return best
    # dead ends: reachable states from which the target can never escape
    good = {st for st in seen if can_exit(*st)}
    stack = list(good)
    while stack:
        for pr in preds.get(stack.pop(), []):
            if pr not in good:
                good.add(pr)
                stack.append(pr)
    return best, len(seen) - len(good), len(seen)


def draw(lv):
    g = [["." for _ in range(N)] for _ in range(N)]
    for c in lv["cars"]:
        ch = c.color[0].upper() if c.target else c.color[0]
        for a, b in cells(c, c.x, c.y):
            if g[b][a] != ".":
                print(f"  !! overlap at {a},{b}")
            g[b][a] = ch
    return "\n".join("  " + " ".join(r) for r in reversed(g))


def main():
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT
    ok = True
    for i, lv in enumerate(parse(path.read_text()), 1):
        for c in lv["cars"]:
            if any(not (0 <= a < N and 0 <= b < N) for a, b in cells(c, c.x, c.y)):
                print(f"  !! {c.color} out of bounds")
        same = {}
        for c in lv["cars"]:
            if not c.target:
                same.setdefault(c.color, set()).add(c.h)
        if any(len(v) > 1 for v in same.values()):
            print("  !! linked cars must share orientation")
        (n, path_), dead, total = solve(lv, count_dead=True)
        nn, _ = solve(lv, naive=True)
        print(f"L{i} {lv['name']}: exit={lv['exit']} notch={lv['notch']}")
        print(draw(lv))
        print(f"  real: {n} drags {path_}")
        print(f"  naive: {nn}   dead-end states: {dead}/{total}\n")
        ok &= n is not None
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
