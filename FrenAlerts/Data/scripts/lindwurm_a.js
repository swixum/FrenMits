const headSignState = {
  "stack": "00A1",
  "tankbuster": "0158",
  "cellChain": "0291",
  "slaughterStack": "013D",
  "slaughterSpread": "0177",
  "cellChainTether": "016E",
  "sharedTankbuster": "0256",
  "lockedTether": "0175",
  "projectionTether": "016F",
  "manaBurstTether": "0170",
  "heavySlamTether": "0171",
  "fireballSplashTether": "0176"
};
const centers = {
  x: 100,
  y: 100
};
const stageTable = {
  "BEC0": "curtainCall",
  "B4C6": "slaughtershed",
  "B509": "idyllic"
};

function m12sScanOrb() {
  let pl = 0, pr = 0, gl = 0, gr = 0;
  const orb = bodiesByBase("19200,19201");
  for (let i = 0; i < orb.length; i++) {
    const o = orb[i];
    if (Math.abs(o.x - 100) < 2) continue;
    const port = o.x < 100;
    if (o.base === 19200) { if (port) pl++; else pr++; }
    else if (o.base === 19201) { if (port) gl++; else gr++; }
  }
  return { pl, pr, gl, gr, n: orb.length };
}

function m12sDecidePurplePort(s) {
  if (s.pl > s.pr) return true;
  if (s.pr > s.pl) return false;
  if (s.pl + s.pr === 0) {
    if (s.gr > s.gl) return true;
    if (s.gl > s.gr) return false;
  }
  return void 0;
}

function m12sOrbSay(pull, voice, purpleIsPort) {
  voice.responseOutputStrings = {
    purpleLeft:  { en: "Purple Orb: Left",  de: "Lila Orb: Links",   cn: "\u7D2B\u7403: \u5DE6", ko: "\uBCF4\uB77C \uC624\uBE0C: \uC67C\uCABD" },
    purpleRight: { en: "Purple Orb: Right", de: "Lila Orb: Rechts",  cn: "\u7D2B\u7403: \u53F3", ko: "\uBCF4\uB77C \uC624\uBE0C: \uC624\uB978\uCABD" },
    greenLeft:   { en: "Green Orb: Left",   de: "Gr\xFCner Orb: Links",  cn: "\u7EFF\u7403: \u5DE6", ko: "\uCD08\uB85D \uC624\uBE0C: \uC67C\uCABD" },
    greenRight:  { en: "Green Orb: Right",  de: "Gr\xFCner Orb: Rechts", cn: "\u7EFF\u7403: \u53F3", ko: "\uCD08\uB85D \uC624\uBE0C: \uC624\uB978\uCABD" }
  };
  if (pull.role === "tank")
    return { alertText: purpleIsPort ? voice.purpleLeft() : voice.purpleRight() };
  return { infoText: purpleIsPort ? voice.greenRight() : voice.greenLeft() };
}

function m12sScanAttempts(labels, pull, voice) {
  if (pull.mortalSlayerDecided) return;
  const s = m12sScanOrb();
  const decs = m12sDecidePurplePort(s);
  consol.log("MortalSlayer scan@" + labels + " P(L/R)=" + s.pl + "/" + s.pr +
    " G(L/R)=" + s.gl + "/" + s.gr + " orbs=" + s.n + " => purpleIsLeft=" + decs +
    " role=" + pull.role);
  if (decs === void 0) return;
  pull.mortalSlayerDecided = true;
  pull.mortalSlayerPurpleIsLeft = decs;
  return m12sOrbSay(pull, voice, decs);
}

function m12sDumpBodies(labels) {
  const bodies = bodiesAll();
  let s = "ActorsDump@" + labels + " n=" + bodies.length;
  for (let i = 0; i < bodies.length; i++) {
    const a = bodies[i];
    s += " | " + a.n + " b=" + a.base + " (" + a.x + "," + a.y + ") h=" + a.h;
  }
  consol.log(s);
}

function m12sReachFlankFromChant(pull, casterCodeHex, labels) {
  const srcCode = parseInt(String(casterCodeHex), 16);
  const bodies = bodiesAll();
  let arms = null;
  for (let i = 0; i < bodies.length; i++) {
    if (bodies[i].e === srcCode) { arms = bodies[i]; break; }
  }
  if (!arms) {
    consol.log("ReachCast@" + labels + " caster=" + casterCodeHex + " NOT in object table (n=" + bodies.length + ")");
    return pull.ravenousReach1SafeSide;
  }
  const dxBit = arms.x - 100;
  const adxs = dxBit < 0 ? -dxBit : dxBit;
  const dir8s = ((Math.round(4 - 4 * arms.h / Math.PI) % 8) + 8) % 8;
  const faceEasts = dir8s >= 1 && dir8s <= 3;
  const faceWests = dir8s >= 5 && dir8s <= 7;
  let flank;
  if (adxs > 3) flank = dxBit < 0 ? "west" : "east";
  else if (faceEasts) flank = "east";
  else if (faceWests) flank = "west";
  consol.log("ReachCast@" + labels + " caster=" + arms.n + " x=" + arms.x + " dx=" + dxBit.toFixed(1) +
    " h=" + arms.h + " dir8=" + dir8s + " => SAFE=" + (flank || "?"));
  if (flank) pull.ravenousReach1SafeSide = flank;
  return flank;
}

defineDuty({
  id: "AacHeavyweightM4SavageP1",
  name: "M12S PT1 - Lindwurm",
  category: "Savage \u2013 Dawntrail",
  zoneId: 1327,
  boss: "Lindwurm",
  center: { x: 100, y: 100 },
  state: {
    phase: "doorboss",
    mortalSlayerGreenLeft: 0,
    mortalSlayerGreenRight: 0,
    mortalSlayerDecided: false,
    inLine: {},
    blobTowerDirs: [],
    skinsplitterCount: 0,
    cellChainCount: 0,
    hasRot: false,
    actorPositions: {},
    replicationCounter: 0,
    replication1FireDebuffCounter: 0,
    replication1DarkDebuffCounter: 0,
    replication1FollowUp: false,
    replication2CloneDirNumPlayers: {},
    replication2DirNumAbility: {},
    replication2PlayerAbilities: {},
    replication2PlayerOrder: [],
    replication2AbilityOrder: [],
    netherwrathFollowup: false,
    manaSpheres: {},
    westManaSpheres: {},
    eastManaSpheres: {},
    closeManaSphereIds: [],
    twistedVisionCounter: 0,
    replication3CloneOrder: [],
    replication3CloneDirNumPlayers: {},
    replication4DirNumAbility: {},
    replication4PlayerAbilities: {},
    replication4BossCloneDirNumPlayers: {},
    replication4PlayerOrder: [],
    replication4AbilityOrder: [],
    cosmicKissPattern: [],
    hasLightResistanceDown: false,
    twistedVision4MechCounter: 0,
    doomPlayers: [],
    hasPyretic: false
  },
  mechanics: [
    raws({
      id: "M12S Phase Tracker",
      type: "StartsUsing",
      netRegex: { id: Object.keys(stageTable), source: "Lindwurm" },
      suppressSeconds: 1,
      run: (pull, hit) => {
        const stage = stageTable[hit.id];
        if (stage === void 0)
          throw new UnreachableCod();
        pull.phase = stage;
        pull.ravenousReach1SafeSide = void 0;
        consol.log("Phase => " + stage + " (cast id=" + hit.id + ")");
      }
    }),
    raws({
      id: "M12S ActorSetPos Tracker",
      type: "ActorSetPos",
      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      run: (pull, hit) => pull.actorPositions[hit.id] = {
        x: parseFloat(hit.x),
        y: parseFloat(hit.y),
        heading: parseFloat(hit.heading)
      }
    }),
    raws({
      id: "M12S ActorMove Tracker",
      type: "ActorMove",
      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      run: (pull, hit) => pull.actorPositions[hit.id] = {
        x: parseFloat(hit.x),
        y: parseFloat(hit.y),
        heading: parseFloat(hit.heading)
      }
    }),
    raws({
      id: "M12S AddedCombatant Tracker",
      type: "AddedCombatant",
      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      run: (pull, hit) => pull.actorPositions[hit.id] = {
        x: parseFloat(hit.x),
        y: parseFloat(hit.y),
        heading: parseFloat(hit.heading)
      }
    }),
    whenChant("B4D7").label("Big Raidwide (B4D7)").alert("Big Raidwide").hold(4.7),
    raws({
      id: "M12S Mortal Slayer Collect",
      type: "AddedCombatant",
      netRegex: { name: "Lindwurm", npcBaseId: ["19200", "19201"], capture: true },
      run: (pull, hit) => {
        const x = parseFloat(hit.x);
        if (isNaN(x) || Math.abs(x - 100) < 2) return;
        const port = x < 100;
        if (hit.npcBaseId === "19201") {
          if (port) pull.mortalSlayerGreenLeft = pull.mortalSlayerGreenLeft + 1;
          else pull.mortalSlayerGreenRight = pull.mortalSlayerGreenRight + 1;
        } else if (hit.npcBaseId === "19200") {
          if (port) pull.mortalSlayerPurpleLeft = (pull.mortalSlayerPurpleLeft || 0) + 1;
          else pull.mortalSlayerPurpleRight = (pull.mortalSlayerPurpleRight || 0) + 1;
        }
      }
    }),
    raws({
      id: "M12S Mortal Slayer Reset",
      type: "StartsUsing",
      netRegex: { id: "B495", capture: false },
      suppressSeconds: 5,
      run: (pull) => {
        pull.mortalSlayerDecided = false;
        pull.mortalSlayerGreenLeft = 0;
        pull.mortalSlayerGreenRight = 0;
        pull.mortalSlayerPurpleLeft = 0;
        pull.mortalSlayerPurpleRight = 0;
        delete pull.mortalSlayerPurpleIsLeft;
      }
    }),
    raws({
      id: "M12S Mortal Slayer Tank Side A",
      type: "StartsUsing",
      netRegex: { id: "B495", capture: true },
      delaySeconds: (_s, m) => { const ctBit = parseFloat(m.castTime); return isNaN(ctBit) ? 2 : Math.max(0.3, ctBit - 1.5); },
      suppressSeconds: 5,
      response: (pull, _h, voice) => m12sScanAttempts("A", pull, voice)
    }),
    raws({
      id: "M12S Mortal Slayer Tank Side B",
      type: "StartsUsing",
      netRegex: { id: "B495", capture: true },
      delaySeconds: (_s, m) => { const ctBit = parseFloat(m.castTime); return isNaN(ctBit) ? 3 : Math.max(0.6, ctBit - 0.5); },
      suppressSeconds: 5,
      response: (pull, _h, voice) => m12sScanAttempts("B", pull, voice)
    }),
    raws({
      id: "M12S Mortal Slayer Tank Side C",
      type: "StartsUsing",
      netRegex: { id: "B495", capture: true },
      delaySeconds: (_s, m) => { const ctBit = parseFloat(m.castTime); return isNaN(ctBit) ? 4 : ctBit + 0.5; },
      suppressSeconds: 5,
      response: (pull, _h, voice) => m12sScanAttempts("C", pull, voice)
    }),
    raws({
      id: "M12S Mortal Slayer Tank Side D",
      type: "StartsUsing",
      netRegex: { id: "B495", capture: true },
      delaySeconds: (_s, m) => { const ctBit = parseFloat(m.castTime); return isNaN(ctBit) ? 5 : ctBit + 1.5; },
      suppressSeconds: 5,
      response: (pull, _h, voice) => m12sScanAttempts("D", pull, voice)
    }),
    raws({
      id: "M12S CombatantMemory Blob Tracker",
      type: "CombatantMemory",
      netRegex: {
        change: "Add",
        pair: [{ key: "BNpcID", value: "1EBF29" }],
        capture: true
      },
      run: (pull, hit) => {
        if (pull.splattershedStackDir)
          return;
        const x = parseFloat(hit.pairPosX ?? "0");
        const y = parseFloat(hit.pairPosY ?? "0");
        if (pull.act1SafeCorner === void 0 && y > 87.9 && y < 89.7) {
          if (x > 112)
            pull.act1SafeCorner = "northwest";
          else if (x < 89)
            pull.act1SafeCorner = "northeast";
        } else if (pull.act1SafeCorner !== void 0 && pull.curtainCallSafeCorner === void 0 && y > 86.5 && y < 87.5) {
          if (x < 92)
            pull.curtainCallSafeCorner = "northeast";
          else if (x > 109)
            pull.curtainCallSafeCorner = "northwest";
        } else if (pull.act1SafeCorner !== void 0 && pull.curtainCallSafeCorner !== void 0 && y > 96 && y < 97) {
          if (x > 88.75 && x < 89.75)
            pull.splattershedStackDir = "northwest";
          else if (x > 110.25 && x < 111.25)
            pull.splattershedStackDir = "northeast";
        }
      }
    }),
    raws({
      id: "M12S Directed Grotesquerie Direction Collect",
      type: "GainsEffect",
      netRegex: { effectId: "DE6", capture: true },
      condition: Condition.targetIsYou(),
      run: (pull, hit) => {
        switch (hit.count) {
          case "40C":
            pull.grotesquerieCleave = "frontCleave";
            return;
          case "40D":
            pull.grotesquerieCleave = "rightCleave";
            return;
          case "40E":
            pull.grotesquerieCleave = "rearCleave";
            return;
          case "40F":
            pull.grotesquerieCleave = "leftCleave";
            return;
        }
      }
    }),
    raws({
      id: "M12S Shared Grotesquerie",
      type: "GainsEffect",
      netRegex: { effectId: "129A", capture: true },
      delaySeconds: 0.2,
      durationSeconds: 17,
      infoText: (pull, hit, voice) => {
        const sweep = pull.grotesquerieCleave;
        const markThree = hit.target;
        const mkPile = (stack) => sweep === void 0
          ? voice.baitThenStack({ stack })
          : voice.baitThenStackCleave({ stack, cleave: voice[sweep]() });
        let body;
        if (markThree === pull.me) {
          body = mkPile(voice.stackOnYou());
        } else {
          const player = pull.party.member(markThree);
          const isDPSBit = pull.party.isDPS(markThree);
          if ((isDPSBit && pull.role === "dps") || (!isDPSBit && pull.role !== "dps"))
            body = mkPile(voice.stackOnPlayer({ player }));
        }
        if (body === void 0)
          return;
        const flank = pull.ravenousReach1SafeSide;
        if (flank)
          return voice.withSide({ side: voice[flank](), body });
        return body;
      },
      outputStrings: {
        stackOnYou: Voices.stackOnYou,
        stackOnPlayer: Voices.stackOnPlayer,
        west: Voices.west,
        east: Voices.east,
        withSide: {
          en: "${side}: ${body}",
          de: "${side}: ${body}",
          cn: "${side}: ${body}",
          ko: "${side}: ${body}"
        },
        frontCleave: {
          en: "Front Cleave",
          de: "Kegel Aoe nach Vorne",
          fr: "Cleave Avant",
          ja: "\u53E3\u304B\u3089\u304A\u304F\u3073",
          cn: "\u5411\u524D\u5C04",
          ko: "\uC804\uBC29 \uBD80\uCC44\uAF34",
          tc: "\u524D\u65B9\u6247\u5F62"
        },
        rearCleave: {
          en: "Rear Cleave",
          de: "Kegel Aoe nach Hinten",
          fr: "Cleave Arri\xE8re",
          ja: "\u5C3B\u304B\u3089\u304A\u306A\u3089",
          cn: "\u5411\u540E\u5C04",
          ko: "\uD6C4\uBC29 \uBD80\uCC44\uAF34",
          tc: "\u80CC\u5F8C\u6247\u5F62"
        },
        leftCleave: {
          en: "Left Cleave",
          de: "Linker Cleave",
          fr: "Cleave gauche",
          ja: "\u5DE6\u534A\u9762\u3078\u653B\u6483",
          cn: "\u5411\u5DE6\u5C04",
          ko: "\uC67C\uCABD \uBD80\uCC44\uAF34",
          tc: "\u5DE6\u5200"
        },
        rightCleave: {
          en: "Right Cleave",
          de: "Rechter Cleave",
          fr: "Cleave droit",
          ja: "\u53F3\u534A\u9762\u3078\u653B\u6483",
          cn: "\u5411\u53F3\u5C04",
          ko: "\uC624\uB978\uCABD \uBD80\uCC44\uAF34",
          tc: "\u53F3\u5200"
        },
        baitThenStack: {
          en: "Bait 4x Puddles => ${stack}",
          de: "K\xF6dere Fl\xE4che x4 => ${stack}",
          cn: "\u8BF1\u5BFC4\u8F6E\u9EC4\u5708 => ${stack}",
          ko: "\uC7A5\uD310 \uC720\uB3C4 4x => ${stack}"
        },
        baitThenStackCleave: {
          en: "Bait 4x Puddles => ${stack} + ${cleave}",
          de: "K\xF6dere Fl\xE4che x4 => ${stack} + ${cleave}",
          cn: "\u8BF1\u5BFC4\u8F6E\u9EC4\u5708 + ${stack} + ${cleave}",
          ko: "\uC7A5\uD310 \uC720\uB3C4 4x => ${stack} + ${cleave}"
        }
      }
    }),
    raws({
      id: "M12S Bursting Grotesquerie",
      type: "GainsEffect",
      netRegex: { effectId: "1299", capture: true },
      condition: Condition.targetIsYou(),
      delaySeconds: 0.2,
      durationSeconds: 17,
      infoText: (pull, _hit, voice) => {
        const sweep = pull.grotesquerieCleave;
        const flank = pull.ravenousReach1SafeSide;
        if (pull.phase !== "doorboss") {
          if (flank)
            return voice.withSide({ side: voice[flank](), body: voice.spreadCurtain() });
          return voice.spreadCurtain();
        }
        let body = sweep === void 0
          ? voice.baitThenSpread()
          : voice.baitThenSpreadCleave({ cleave: voice[sweep]() });
        if (flank)
          return voice.withSide({ side: voice[flank](), body });
        return body;
      },
      outputStrings: {
        west: Voices.west,
        east: Voices.east,
        withSide: {
          en: "${side}: ${body}",
          de: "${side}: ${body}",
          cn: "${side}: ${body}",
          ko: "${side}: ${body}"
        },
        frontCleave: {
          en: "Front Cleave",
          de: "Kegel Aoe nach Vorne",
          fr: "Cleave Avant",
          ja: "\u53E3\u304B\u3089\u304A\u304F\u3073",
          cn: "\u5411\u524D\u5C04",
          ko: "\uC804\uBC29 \uBD80\uCC44\uAF34",
          tc: "\u524D\u65B9\u6247\u5F62"
        },
        rearCleave: {
          en: "Rear Cleave",
          de: "Kegel Aoe nach Hinten",
          fr: "Cleave Arri\xE8re",
          ja: "\u5C3B\u304B\u3089\u304A\u306A\u3089",
          cn: "\u5411\u540E\u5C04",
          ko: "\uD6C4\uBC29 \uBD80\uCC44\uAF34",
          tc: "\u80CC\u5F8C\u6247\u5F62"
        },
        leftCleave: {
          en: "Left Cleave",
          de: "Linker Cleave",
          fr: "Cleave gauche",
          ja: "\u5DE6\u534A\u9762\u3078\u653B\u6483",
          cn: "\u5411\u5DE6\u5C04",
          ko: "\uC67C\uCABD \uBD80\uCC44\uAF34",
          tc: "\u5DE6\u5200"
        },
        rightCleave: {
          en: "Right Cleave",
          de: "Rechter Cleave",
          fr: "Cleave droit",
          ja: "\u53F3\u534A\u9762\u3078\u653B\u6483",
          cn: "\u5411\u53F3\u5C04",
          ko: "\uC624\uB978\uCABD \uBD80\uCC44\uAF34",
          tc: "\u53F3\u5200"
        },
        baitThenSpread: {
          en: "Bait 4x Puddles => Spread",
          de: "K\xF6dere Fl\xE4che x4 => Verteilen",
          cn: "\u8BF1\u5BFC4\u8F6E\u9EC4\u5708 => \u5206\u6563",
          ko: "\uC7A5\uD310 \uC720\uB3C4 4x => \uC0B0\uAC1C"
        },
        baitThenSpreadCleave: {
          en: "Bait 4x Puddles => Spread + ${cleave}",
          de: "K\xF6dere Fl\xE4che x4 => Verteilen + ${cleave}",
          cn: "\u8BF1\u5BFC4\u8F6E\u9EC4\u5708 => \u5206\u6563 + ${cleave}",
          ko: "\uC7A5\uD310 \uC720\uB3C4 4x => \uC0B0\uAC1C + ${cleave}"
        },
        spreadCurtain: {
          en: "Spread Debuff on YOU",
          de: "Verteilen Debuff auf DIR",
          cn: "\u5206\u6563 Debuff \u70B9\u540D",
          ko: "\uC0B0\uAC1C\uC9D5 \uB300\uC0C1\uC790"
        }
      }
    }),
    raws({
      id: "M12S Ravenous Reach 1 Safe Side Collect",
      type: "Ability",
      netRegex: { id: ["B49A", "B49B"], source: "Lindwurm", capture: true },
      condition: (pull) => pull.phase === "doorboss",
      run: (pull, hit) => {
        pull.ravenousReach1SafeSide = hit.id === "B49A" ? "west" : "east";
        consol.log("RavenousReach1 head cleave id=" + hit.id +
          " => safe=" + pull.ravenousReach1SafeSide);
      }
    }),
    raws({
      id: "M12S Ravenous Reach 1 Safe Side",
      type: "StartsUsing",
      netRegex: { id: "B49D", source: "Lindwurm", capture: true },
      condition: (pull) => pull.phase === "doorboss" || pull.phase === "curtainCall",
      delaySeconds: 0.8,
      durationSeconds: 7,
      suppressSeconds: 20,
      alertText: (pull, hit, voice) => {
        const flank = pull.ravenousReach1SafeSide ||
          m12sReachFlankFromChant(pull, hit.sourceId, pull.phase === "doorboss" ? "rr1" : "rr2");
        if (!flank)
          return;
        return flank === "west" ? voice.goWest() : voice.goEast();
      },
      outputStrings: {
        goEast: Voices.east,
        goWest: Voices.west
      }
    }),
    raws({
      id: "M12S Act 1 Blob Safe Spots (early)",
      type: "Ability",
      netRegex: { id: "B49D", source: "Lindwurm", capture: false },
      delaySeconds: 0.3,
      suppressSeconds: 9999,
      infoText: (pull, _hit, voice) => {
        const reachs = pull.ravenousReach1SafeSide;
        const dir1s = pull.act1SafeCorner;
        const dir2s = dir1s === "northwest" ? "east" : "west";
        if (pull.role !== "tank") {
          const dir3s = dir1s === void 0 ? dir1s : reachs === dir2s ? dir2s : reachs === dir1s.slice(5) ? dir1s : void 0;
          if (dir1s) {
            if (dir3s) {
              return voice.safeSpot({
                safe: voice[dir3s]()
              });
            }
            return voice.safeSpot({
              safe: voice.safeDirs({
                dir1: voice[dir1s](),
                dir2: voice[dir2s]()
              })
            });
          }
        }
        const facingTwo = dir1s === void 0 ? dir1s : reachs === dir2s ? dir1s : reachs === dir1s.slice(5) ? dir2s : void 0;
        if (dir1s) {
          if (facingTwo) {
            return voice.safeSpot({
              safe: voice[facingTwo]()
            });
          }
          return voice.safeSpot({
            safe: voice.safeDirs({
              dir1: voice[dir1s](),
              dir2: voice[dir2s]()
            })
          });
        }
      },
      outputStrings: {
        northeast: Voices.northeast,
        east: Voices.east,
        west: Voices.west,
        northwest: Voices.northwest,
        safeSpot: {
          en: "${safe} (later)",
          de: "${safe} (sp\xE4ter)",
          cn: "${safe} (\u7A0D\u540E)",
          ko: "${safe} (\uB098\uC911\uC5D0)"
        },
        safeDirs: {
          en: "${dir1}/${dir2}",
          de: "${dir1}/${dir2}",
          cn: "${dir1}/${dir2}",
          ko: "${dir1}/${dir2}"
        }
      }
    }),
    raws({
      id: "M12S Fourth-wall Fusion Stack",
      type: "HeadMarker",
      netRegex: { id: headSignState["stack"], capture: true },
      condition: (pull) => {
        if (pull.role === "tank")
          return false;
        return true;
      },
      durationSeconds: 5.1,
      alertText: (pull, hit, voice) => {
        const reachs = pull.ravenousReach1SafeSide;
        const dir1s = pull.act1SafeCorner;
        const dir2s = dir1s === "northwest" ? "east" : "west";
        const markThree = hit.target;
        const facingTwo = dir1s === void 0 ? dir1s : reachs === dir2s ? dir2s : reachs === dir1s.slice(5) ? dir1s : void 0;
        if (markThree === pull.me) {
          if (dir1s) {
            if (facingTwo) {
              return voice.stackSafe({
                stack: voice.stackOnYou(),
                safe: voice[facingTwo]()
              });
            }
            return voice.stackSafe({
              stack: voice.stackOnYou(),
              safe: voice.stackDirs({
                dir1: voice[dir1s](),
                dir2: voice[dir2s]()
              })
            });
          }
          return voice.stackOnYou();
        }
        const player = pull.party.member(markThree);
        if (dir1s) {
          if (facingTwo) {
            return voice.stackSafe({
              stack: voice.stackOnTarget({ player }),
              safe: voice[facingTwo]()
            });
          }
          return voice.stackSafe({
            stack: voice.stackOnTarget({ player }),
            safe: voice.stackDirs({
              dir1: voice[dir1s](),
              dir2: voice[dir2s]()
            })
          });
        }
        return voice.stackOnTarget({ player });
      },
      outputStrings: {
        northeast: Voices.northeast,
        east: Voices.east,
        west: Voices.west,
        northwest: Voices.northwest,
        stackOnYou: Voices.stackOnYou,
        stackOnTarget: Voices.stackOnPlayer,
        stackSafe: {
          en: "${stack} ${safe}",
          de: "${stack} ${safe}",
          cn: "${stack} ${safe}",
          ko: "${stack} ${safe}"
        },
        stackDirs: {
          en: "${dir1}/${dir2}",
          de: "${dir1}/${dir2}",
          cn: "${dir1}/${dir2}",
          ko: "${dir1}/${dir2}"
        }
      }
    }),
    raws({
      id: "M12S Tankbuster",
      type: "HeadMarker",
      netRegex: { id: headSignState["tankbuster"], capture: true },
      condition: Condition.targetIsYou(),
      durationSeconds: 5.1,
      alertText: (pull, _hit, voice) => {
        const reachs = pull.ravenousReach1SafeSide;
        const dir1s = pull.act1SafeCorner;
        const dir2s = dir1s === "northwest" ? "east" : "west";
        const facingTwo = dir1s === void 0 ? dir1s : reachs === dir2s ? dir1s : reachs === dir1s.slice(5) ? dir2s : void 0;
        if (dir1s) {
          if (facingTwo) {
            return voice.busterSafe({
              buster: voice.busterOnYou(),
              safe: voice[facingTwo]()
            });
          }
          return voice.busterSafe({
            buster: voice.busterOnYou(),
            safe: voice.busterDirs({
              dir1: voice[dir1s](),
              dir2: voice[dir2s]()
            })
          });
        }
        return voice.busterOnYou();
      },
      outputStrings: {
        northeast: Voices.northeast,
        east: Voices.east,
        west: Voices.west,
        northwest: Voices.northwest,
        busterOnYou: Voices.tankBusterOnYou,
        busterSafe: {
          en: "${buster} + ${safe}",
          de: "${buster} + ${safe}",
          cn: "${buster} + ${safe}",
          ko: "${buster} + ${safe}"
        },
        busterDirs: {
          en: "${dir1}/${dir2}",
          de: "${dir1}/${dir2}",
          cn: "${dir1}/${dir2}",
          ko: "${dir1}/${dir2}"
        }
      }
    }),
    raws({
      id: "M12S In Line Debuff Collector",
      type: "GainsEffect",
      netRegex: { effectId: ["BBC", "BBD", "BBE", "D7B"] },
      run: (pull, hit) => {
        const auraToDigit = {
          BBC: 1,
          BBD: 2,
          BBE: 3,
          D7B: 4
        };
        const num = auraToDigit[hit.effectId];
        if (num === void 0)
          return;
        pull.inLine[hit.target] = num;
      }
    }),
    raws({
      id: "M12S Bonds of Flesh Flesh  Alpha/ Beta Collect",
      type: "GainsEffect",
      netRegex: { effectId: ["1290", "1292"], capture: true },
      condition: Condition.targetIsYou(),
      run: (pull, hit) => {
        pull.myFleshBonds = hit.effectId === "1290" ? "alpha" : "beta";
      }
    }),
    raws({
      id: "M12S In Line Debuff",
      type: "GainsEffect",
      netRegex: { effectId: ["BBC", "BBD", "BBE", "D7B"], capture: false },
      delaySeconds: 0.5,
      durationSeconds: 10,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const myDigit = pull.inLine[pull.me];
        if (myDigit === void 0)
          return;
        const fleshs = pull.myFleshBonds;
        if (fleshs === void 0)
          return voice.order({ num: myDigit });
        if (fleshs === "alpha") {
          switch (myDigit) {
            case 1:
              return voice.alpha1();
            case 2:
              return voice.alpha2();
            case 3:
              return voice.alpha3();
            case 4:
              return voice.alpha4();
          }
        }
        switch (myDigit) {
          case 1:
            return voice.beta1();
          case 2:
            return voice.beta2();
          case 3:
            return voice.beta3();
          case 4:
            return voice.beta4();
        }
      },
      tts: (pull, _hit, voice) => {
        const myDigit = pull.inLine[pull.me];
        if (myDigit === void 0)
          return;
        const fleshs = pull.myFleshBonds;
        if (fleshs === void 0)
          return voice.order({ num: myDigit });
        if (fleshs === "alpha") {
          switch (myDigit) {
            case 1:
              return voice.alpha1Tts();
            case 2:
              return voice.alpha2Tts();
            case 3:
              return voice.alpha3Tts();
            case 4:
              return voice.alpha4Tts();
          }
        }
        switch (myDigit) {
          case 1:
            return voice.beta1Tts();
          case 2:
            return voice.beta2Tts();
          case 3:
            return voice.beta3Tts();
          case 4:
            return voice.beta4Tts();
        }
      },
      outputStrings: {
        alpha1: {
          en: "1 Alpha: Wait for Tether 1",
          de: "1 Alpha: Warte auf Verbindung 1",
          cn: "1 Alpha: \u62C9 1 \u7EBF",
          ko: "1 Alpha: \uC120 1 \uAE30\uB2E4\uB9AC\uAE30"
        },
        alpha2: {
          en: "2 Alpha: Wait for Tether 2",
          de: "2 Alpha: Warte auf Verbindung 2",
          cn: "2 Alpha: \u62C9 2 \u7EBF",
          ko: "2 Alpha: \uC120 2 \uAE30\uB2E4\uB9AC\uAE30"
        },
        alpha3: {
          en: "3 Alpha: Blob Tower 1",
          de: "3 Alpha: Blob Turm 1",
          cn: "3 Alpha: \u8E29\u573A\u5730 1 \u5854",
          ko: "3 Alpha: \uC0B4\uC810 \uD0D1 1"
        },
        alpha4: {
          en: "4 Alpha: Blob Tower 2",
          de: "4 Alpha: Blob Turm 2",
          cn: "4 Alpha: \u8E29\u573A\u5730 2 \u5854",
          ko: "4 Alpha: \uC0B4\uC810 \uD0D1 2"
        },
        beta1: {
          en: "1 Beta: Wait for Tether 1",
          de: "1 Beta: Warte auf Verbindung 1",
          cn: "1 Beta: \u53CD\u62C9 1 \u7EBF",
          ko: "1 Beta: \uC120 1 \uAE30\uB2E4\uB9AC\uAE30"
        },
        beta2: {
          en: "2 Beta: Wait for Tether 2",
          de: "2 Beta: Warte auf Verbindung 2",
          cn: "2 Beta: \u53CD\u62C9 2 \u7EBF",
          ko: "2 Beta: \uC120 2 \uAE30\uB2E4\uB9AC\uAE30"
        },
        beta3: {
          en: "3 Beta: Chain Tower 1",
          de: "3 Beta: Ketten Turm 1",
          cn: "3 Beta: \u8E29\u73A9\u5BB6 1 \u5854",
          ko: "3 Beta: \uC124\uCE58\uD55C \uD0D1 1"
        },
        beta4: {
          en: "4 Beta: Chain Tower 2",
          de: "4 Beta: Ketten Turm 2",
          cn: "4 Beta: \u8E29\u73A9\u5BB6 2 \u5854",
          ko: "4 Beta: \uC124\uCE58\uD55C \uD0D1 2"
        },
        alpha1Tts: {
          en: "1 Alpha: Wait for Tether 1",
          de: "1 Alpha: WWarte auf Verbindung 1",
          cn: "1 Alpha: \u62C9 1 \u7EBF",
          ko: "\uC54C\uD30C 1: \uC120 1 \uAE30\uB2E4\uB9AC\uAE30"
        },
        alpha2Tts: {
          en: "2 Alpha: Wait for Tether 2",
          de: "2 Alpha: Warte auf Verbindung 2",
          cn: "2 Alpha: \u62C9 2 \u7EBF",
          ko: "\uC54C\uD30C 2: \uC120 2 \uAE30\uB2E4\uB9AC\uAE30"
        },
        alpha3Tts: {
          en: "3 Alpha: Blob Tower 1",
          de: "3 Alpha: Blob Turm 1",
          cn: "3 Alpha: \u8E29\u573A\u5730 1 \u5854",
          ko: "\uC54C\uD30C 3: \uC0B4\uC810 \uD0D1 1"
        },
        alpha4Tts: {
          en: "4 Alpha: Blob Tower 2",
          de: "4 Alpha: Blob Turm 2",
          cn: "4 Alpha: \u8E29\u573A\u5730 2 \u5854",
          ko: "\uC54C\uD30C 4: \uC0B4\uC810 \uD0D1 2"
        },
        beta1Tts: {
          en: "1 Beta: Wait for Tether 1",
          de: "1 Beta: Warte auf Verbindung 1",
          cn: "1 Beta: \u53CD\u62C9 1 \u7EBF",
          ko: "\uBCA0\uD0C0 1: \uC120 1 \uAE30\uB2E4\uB9AC\uAE30"
        },
        beta2Tts: {
          en: "2 Beta: Wait for Tether 2",
          de: "2 Beta: Warte auf Verbindung 2",
          cn: "2 Beta: \u53CD\u62C9 2 \u7EBF",
          ko: "\uBCA0\uD0C0 2: \uC120 2 \uAE30\uB2E4\uB9AC\uAE30"
        },
        beta3Tts: {
          en: "3 Beta: Chain Tower 1",
          de: "3 Beta: Ketten Turm 1",
          cn: "3 Beta: \u8E29\u73A9\u5BB6 1 \u5854",
          ko: "\uBCA0\uD0C0 3: \uC124\uCE58\uD55C \uD0D1 1"
        },
        beta4Tts: {
          en: "4 Beta: Chain Tower 2",
          de: "4 Beta: Ketten Turm 2",
          cn: "4 Beta: \u8E29\u73A9\u5BB6 2 \u5854",
          ko: "\uBCA0\uD0C0 4: \uC124\uCE58\uD55C \uD0D1 2"
        },
        order: {
          en: "${num}",
          de: "${num}",
          fr: "${num}",
          ja: "${num}",
          cn: "${num}",
          ko: "${num}",
          tc: "${num}"
        },
        unknown: Voices.unknown
      }
    }),
    raws({
      id: "M12S Phagocyte Spotlight Blob Tower Location Collect",
      type: "StartsUsingExtra",
      netRegex: { id: "B4B6", capture: true },
      suppressSeconds: 10,
      run: (pull, hit) => {
        const x = parseFloat(hit.x);
        const y = parseFloat(hit.y);
        const facingTwo = Facings.xyToIntercardDirOutput(x, y, centers.x, centers.y);
        pull.blobTowerDirs.push(facingTwo);
        if (facingTwo === "dirSE") {
          pull.blobTowerDirs.push("dirNW");
          pull.blobTowerDirs.push("dirSW");
          pull.blobTowerDirs.push("dirNE");
        } else if (facingTwo === "dirNE") {
          pull.blobTowerDirs.push("dirSW");
          pull.blobTowerDirs.push("dirNW");
          pull.blobTowerDirs.push("dirSE");
        } else if (facingTwo === "dirNW") {
          pull.blobTowerDirs.push("dirSE");
          pull.blobTowerDirs.push("dirNE");
          pull.blobTowerDirs.push("dirSW");
        } else if (facingTwo === "dirSW") {
          pull.blobTowerDirs.push("dirNE");
          pull.blobTowerDirs.push("dirSE");
          pull.blobTowerDirs.push("dirNW");
        }
      }
    }),
    raws({
      id: "M12S Phagocyte Spotlight Blob Tower Location (Early)",
      type: "StartsUsingExtra",
      netRegex: { id: "B4B6", capture: false },
      condition: (pull) => pull.myFleshBonds === "alpha",
      delaySeconds: 0.1,
      durationSeconds: (pull) => {
        const myDigit = pull.inLine[pull.me];
        switch (myDigit) {
          case 1:
            return 20;
          case 2:
            return 25;
          case 3:
            return 21;
          case 4:
            return 21;
        }
      },
      suppressSeconds: 10,
      infoText: (pull, _hit, voice) => {
        const myDigit = pull.inLine[pull.me];
        if (myDigit === void 0)
          return;
        const myDigitToFacingSpot = {
          1: 2,
          2: 3,
          3: 0,
          4: 1
        };
        const facingSpot = myDigitToFacingSpot[myDigit];
        if (facingSpot === void 0)
          return;
        const pillarDigit = facingSpot + 1;
        const facingTwo = pull.blobTowerDirs[facingSpot];
        if (facingTwo === void 0)
          return;
        if (myDigit > 2)
          return voice.innerBlobTower({
            num: pillarDigit,
            dir: voice[facingTwo]()
          });
        return voice.outerBlobTower({ num: pillarDigit, dir: voice[facingTwo]() });
      },
      outputStrings: {
        ...Facings.outputStringsIntercardDir,
        innerBlobTower: {
          en: "Blob Tower ${num} Inner ${dir} (later)",
          de: "Blob Turm ${num} Innen ${dir} (sp\xE4ter)",
          cn: "\u8E29\u573A\u5185${dir}\u573A\u5730${num}\u5854 (\u7A0D\u540E)",
          ko: "\uC0B4\uC810 \uD0D1 ${num} \uC548\uCABD ${dir} (\uB098\uC911\uC5D0)"
        },
        outerBlobTower: {
          en: "Blob Tower ${num} Outer ${dir} (later)",
          de: "Blob Turm ${num} Au\xDFen ${dir} (sp\xE4ter)",
          cn: "\u8E29\u573A\u5916${dir}\u573A\u5730${num}\u5854 (\u7A0D\u540E)",
          ko: "\uC0B4\uC810 \uD0D1 ${num} \uBC14\uAE65\uCABD ${dir} (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M12S Cursed Coil Bind Draw-in",
      type: "Ability",
      netRegex: { id: "B4B6", capture: false },
      delaySeconds: 3,
      suppressSeconds: 10,
      response: Response.drawIn()
    }),
    raws({
      id: "M12S Cursed Coil Initial Direction Collect",
      type: "StartsUsing",
      netRegex: { id: ["B4B8", "B4B9", "B4BA", "B4BB"], source: "Lindwurm", capture: true },
      run: (pull, hit) => {
        switch (hit.id) {
          case "B4B8":
            pull.cursedCoilDirNum = 1;
            return;
          case "B4B9":
            pull.cursedCoilDirNum = 3;
            return;
          case "B4BA":
            pull.cursedCoilDirNum = 0;
            return;
          case "B4BB":
            pull.cursedCoilDirNum = 2;
        }
      }
    }),
    raws({
      id: "M12S Skinsplitter Counter",
      type: "Ability",
      netRegex: { id: "B4BC", capture: false },
      suppressSeconds: 1,
      run: (pull) => pull.skinsplitterCount = pull.skinsplitterCount + 1
    }),
    raws({
      id: "M12S Cell Chain Counter",
      type: "Tether",
      netRegex: { id: headSignState["cellChainTether"], capture: false },
      condition: (pull) => pull.phase === "doorboss",
      run: (pull) => pull.cellChainCount = pull.cellChainCount + 1
    }),
    raws({
      id: "M12S Cell Chain Tether Number",
      type: "Tether",
      netRegex: { id: headSignState["cellChainTether"], capture: false },
      condition: (pull) => {
        if (pull.phase === "doorboss" && pull.myFleshBonds === "beta")
          return true;
        return false;
      },
      infoText: (pull, _hit, voice) => {
        const myDigit = pull.inLine[pull.me];
        const num = pull.cellChainCount;
        if (myDigit !== num) {
          if (myDigit === 1 && num === 3)
            return voice.beta1Tower({
              tether: voice.tether({ num })
            });
          if (myDigit === 2 && num === 4)
            return voice.beta2Tower({
              tether: voice.tether({ num })
            });
          if (myDigit === 3 && num === 1)
            return voice.beta3Tower({
              tether: voice.tether({ num })
            });
          if (myDigit === 4 && num === 2)
            return voice.beta4Tower({
              tether: voice.tether({ num })
            });
          return voice.tether({ num });
        }
        if (myDigit === void 0)
          return voice.tether({ num });
      },
      outputStrings: {
        tether: {
          en: "Tether ${num}",
          de: "Verbindung ${num}",
          fr: "Lien ${num}",
          ja: "\u7DDA ${num}",
          cn: "\u62C9${num}\u7EBF",
          ko: "\uC120 ${num}",
          tc: "\u7DDA ${num}"
        },
        beta1Tower: {
          en: "${tether} => Chain Tower 3",
          de: "${tether} => Ketten Turm 3",
          cn: "${tether} => \u73A9\u5BB63\u5854",
          ko: "${tether} => \uC124\uCE58\uD55C \uD0D1 3"
        },
        beta2Tower: {
          en: "${tether} => Chain Tower 4",
          de: "${tether} => Ketten Turm 4",
          cn: "${tether} => \u73A9\u5BB64\u5854",
          ko: "${tether} => \uC124\uCE58\uD55C \uD0D1 4"
        },
        beta3Tower: {
          en: "${tether} => Chain Tower 1",
          de: "${tether} => Ketten Turm 1",
          cn: "${tether} => \u73A9\u5BB61\u5854",
          ko: "${tether} => \uC124\uCE58\uD55C \uD0D1 1"
        },
        beta4Tower: {
          en: "${tether} => Chain Tower 2",
          de: "${tether} => Ketten Turm 2",
          cn: "${tether} => \u73A9\u5BB62\u5854",
          ko: "${tether} => \uC124\uCE58\uD55C \uD0D1 2"
        }
      }
    }),
    raws({
      id: "M12S Chain Tower Number",
      type: "Ability",
      netRegex: { id: "B4B4", capture: false },
      condition: (pull) => {
        if (pull.phase === "doorboss" && pull.myFleshBonds === "beta")
          return true;
        return false;
      },
      suppressSeconds: 1,
      alertText: (pull, _hit, voice) => {
        const mechanicDigit = pull.cellChainCount;
        const myDigit = pull.inLine[pull.me];
        if (myDigit === void 0)
          return;
        const myDigitToSequence = {
          1: 3,
          2: 4,
          3: 1,
          4: 2
        };
        const mySequence = myDigitToSequence[myDigit];
        if (mySequence === void 0)
          return;
        if (mySequence === mechanicDigit)
          return voice.tower({ num: mechanicDigit });
      },
      outputStrings: {
        tower: {
          en: "Get Chain Tower ${num}",
          de: "Nimm Ketten Turm ${num}",
          cn: "\u8E29\u73A9\u5BB6${num}\u5854",
          ko: "\uC124\uCE58\uD55C \uD0D1 ${num} \uBC1F\uAE30"
        }
      }
    }),
    raws({
      id: "M12S Bonds of Flesh Flesh  Alpha First Two Towers",
      type: "GainsEffect",
      netRegex: { effectId: "1290", capture: true },
      condition: (pull, hit) => {
        if (hit.target === pull.me) {
          const durations = parseFloat(hit.duration);
          if (durations < 35)
            return false;
          return true;
        }
        return false;
      },
      delaySeconds: (_pull, hit) => {
        const durations = parseFloat(hit.duration);
        if (durations > 37)
          return 31;
        return 26;
      },
      alertText: (pull, hit, voice) => {
        const durations = parseFloat(hit.duration);
        const facingTwo = pull.blobTowerDirs[durations > 40 ? 1 : 0];
        if (durations > 40) {
          if (facingTwo !== void 0)
            return voice.alpha4Dir({ dir: voice[facingTwo]() });
          return voice.alpha4();
        }
        if (facingTwo !== void 0)
          return voice.alpha3Dir({ dir: voice[facingTwo]() });
        return voice.alpha3();
      },
      outputStrings: {
        ...Facings.outputStringsIntercardDir,
        alpha3: {
          en: "Get Blob Tower 1",
          de: "Nimm Blob Turm 1",
          cn: "\u8E29\u573A\u57301\u5854",
          ko: "\uC0B4\uC810 \uD0D1 1 \uBC1F\uAE30"
        },
        alpha4: {
          en: "Get Blob Tower 2",
          de: "Nimm Blob Turm 2",
          cn: "\u8E29\u573A\u57302\u5854",
          ko: "\uC0B4\uC810 \uD0D1 2 \uBC1F\uAE30"
        },
        alpha3Dir: {
          en: "Get Blob Tower 1 (Inner ${dir})",
          de: "Nimm Blob Turm 1 (Innen ${dir})",
          cn: "\u8E29\u573A\u57301\u5854 (\u5185${dir})",
          ko: "\uC0B4\uC810 \uD0D1 1 (\uC548\uCABD ${dir}) \uBC1F\uAE30"
        },
        alpha4Dir: {
          en: "Get Blob Tower 2 (Inner ${dir})",
          de: "Nimm Blob Turm 2 (Innen ${dir})",
          cn: "\u8E29\u573A\u57302\u5854 (\u5185${dir})",
          ko: "\uC0B4\uC810 \uD0D1 2 (\uC548\uCABD ${dir}) \uBC1F\uAE30"
        }
      }
    }),
    raws({
      id: "M12S Unbreakable Flesh  Alpha/ Beta Chains and Last Two Towers",
      type: "GainsEffect",
      netRegex: { effectId: ["1291", "1293"], capture: true },
      condition: (pull, hit) => {
        if (hit.target === pull.me && pull.phase === "doorboss")
          return true;
        return false;
      },
      durationSeconds: 11,
      alertText: (pull, hit, voice) => {
        const myDigit = pull.inLine[pull.me];
        const fleshs = hit.effectId === "1291" ? "alpha" : "beta";
        const coilFacingDigit = pull.cursedCoilDirNum !== void 0 ? (pull.cursedCoilDirNum - pull.skinsplitterCount + 8) % 4 : void 0;
        if (fleshs === "alpha") {
          const exits = Facings.outputCardinalDir[coilFacingDigit ?? 4] ?? "unknown";
          if (myDigit === 1) {
            const dir2s = pull.blobTowerDirs[2];
            if (dir2s !== void 0)
              return voice.alpha1Dir({
                chains: voice.breakChains(),
                exit: voice[exits](),
                dir: voice[dir2s]()
              });
          }
          if (myDigit === 2) {
            const dir2s = pull.blobTowerDirs[3];
            if (dir2s !== void 0)
              return voice.alpha2Dir({
                chains: voice.breakChains(),
                exit: voice[exits](),
                dir: voice[dir2s]()
              });
          }
          switch (myDigit) {
            case 1:
              return voice.alpha1({
                chains: voice.breakChains(),
                exit: voice[exits]()
              });
            case 2:
              return voice.alpha2({
                chains: voice.breakChains(),
                exit: voice[exits]()
              });
            case 3:
              return voice.alpha3({
                chains: voice.breakChains(),
                exit: voice[exits]()
              });
            case 4:
              return voice.alpha4({
                chains: voice.breakChains(),
                exit: voice[exits]()
              });
          }
        }
        const facingTwo = coilFacingDigit !== void 0 ? Facings.outputCardinalDir[(coilFacingDigit + 2) % 4] ?? "unknown" : "unknown";
        switch (myDigit) {
          case 1:
            return voice.beta1({
              chains: voice.breakChains(),
              dir: voice[facingTwo]()
            });
          case 2:
            return voice.beta2({
              chains: voice.breakChains(),
              dir: voice[facingTwo]()
            });
          case 3:
            return voice.beta3({
              chains: voice.breakChains(),
              dir: voice[facingTwo]()
            });
          case 4:
            return voice.beta4({
              chains: voice.breakChains(),
              dir: voice[facingTwo]()
            });
        }
        return voice.getTowers();
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        breakChains: Voices.breakChains,
        getTowers: Voices.getTowers,
        alpha1: {
          en: "${chains} 1 (${exit}) + Blob Tower 3 (Outer)",
          de: "${chains} 1 (${exit}) + Blob Turm 3 (Au\xDFen)",
          cn: "${chains} 1 (${exit}) + \u573A\u57303\u5854 (\u573A\u5916)",
          ko: "${chains} 1 (${exit}) + \uC0B4\uC810 \uD0D1 3 (\uBC14\uAE65\uCABD)"
        },
        alpha1Dir: {
          en: "${chains} 1 (${exit}) + Blob Tower 3 (Outer ${dir})",
          de: "${chains} 1 (${exit}) + Blob Turm 3 (Au\xDFen ${dir})",
          cn: "${chains} 1 (${exit}) + \u573A\u57303\u5854 (\u573A\u5916 ${dir})",
          ko: "${chains} 1 (${exit}) + \uC0B4\uC810 \uD0D1 3 (\uBC14\uAE65\uCABD ${dir})"
        },
        alpha1ExitDir: {
          en: "${chains} 1 (${exit}) + Blob Tower 3 (Outer ${dir})",
          de: "${chains} 1 (${exit}) + Blob Turm 3 (Au\xDFen ${dir})",
          cn: "${chains} 1 (${exit}) + \u573A\u57303\u5854 (\u573A\u5916 ${dir})",
          ko: "${chains} 1 (${exit}) + \uC0B4\uC810 \uD0D1 3 (\uBC14\uAE65\uCABD ${dir})"
        },
        alpha2: {
          en: "${chains} 2 (${exit}) + Blob Tower 4 (Outer)",
          de: "${chains} 2 (${exit}) + Blob Turm 4 (Au\xDFen)",
          cn: "${chains} 2 (${exit}) + \u573A\u57304\u5854 (\u573A\u5916)",
          ko: "${chains} 2 (${exit}) + \uC0B4\uC810 \uD0D1 4 (\uBC14\uAE65\uCABD)"
        },
        alpha2Dir: {
          en: "${chains} 2 (${exit}) + Blob Tower 4 (Outer ${dir})",
          de: "${chains} 2 (${exit}) + Blob Turm 4 (Au\xDFen ${dir})",
          cn: "${chains} 2 (${exit}) + \u573A\u57304\u5854 (\u573A\u5916 ${dir})",
          ko: "${chains} 2 (${exit}) + \uC0B4\uC810 \uD0D1 4 (\uBC14\uAE65\uCABD ${dir})"
        },
        alpha3: {
          en: "${chains} 3 (${exit}) + Get Out",
          de: "${chains} 3 (${exit}) + Geh Raus",
          cn: "${chains} 3 (${exit}) + \u51FA\u53BB",
          ko: "${chains} 3 (${exit}) + \uBC16\uC73C\uB85C"
        },
        alpha4: {
          en: "${chains} 4 (${exit}) + Get Out",
          de: "${chains} 4 (${exit}) + Geh Raus",
          cn: "${chains} 4 (${exit}) + \u51FA\u53BB",
          ko: "${chains} 4 (${exit}) + \uBC16\uC73C\uB85C"
        },
        beta1: {
          en: "${chains} 1 (${dir}) => Get Middle",
          de: "${chains} 1 (${dir}) => Geh Mitte",
          cn: "${chains} 1 (${dir}) => \u4E2D\u95F4",
          ko: "${chains} 1 (${dir}) => \uC911\uC559\uC73C\uB85C"
        },
        beta2: {
          en: "${chains} 2 (${dir}) => Get Middle",
          de: "${chains} 2 (${dir}) => Geh Mitte",
          cn: "${chains} 2 (${dir}) => \u4E2D\u95F4",
          ko: "${chains} 2 (${dir}) => \uC911\uC559\uC73C\uB85C"
        },
        beta3: {
          en: "${chains} 3 (${dir}) => Wait for last pair",
          de: "${chains} 3 (${dir}) => Warte f\xFCrs letzte Paar",
          cn: "${chains} 3 (${dir}) => \u7B49\u5F85\u6700\u540E\u4E00\u7EC4",
          ko: "${chains} 3 (${dir}) => \uB9C8\uC9C0\uB9C9 \uC30D \uAE30\uB2E4\uB9AC\uAE30"
        },
        beta4: {
          en: "${chains} 4 (${dir}) => Get Out",
          de: "${chains} 4 (${dir}) => Geh Raus",
          cn: "${chains} 4 (${dir}) => \u51FA\u53BB",
          ko: "${chains} 4 (${dir}) => \uBC16\uC73C\uB85C"
        }
      }
    }),
    raws({
      id: "M12S Chain Tower Followup",
      type: "Ability",
      netRegex: { id: "B4B3", capture: true },
      condition: (pull, hit) => {
        if (pull.myFleshBonds === "beta" && pull.me === hit.target)
          return true;
        return false;
      },
      infoText: (pull, _hit, voice) => {
        const mechanicDigit = pull.skinsplitterCount;
        const myDigit = pull.inLine[pull.me];
        if (myDigit === void 0) {
          if (mechanicDigit < 5)
            return voice.goIntoMiddle();
          return voice.getOut();
        }
        if (mechanicDigit < 5) {
          if (myDigit === 1)
            return voice.beta1Middle();
          if (myDigit === 2)
            return voice.beta2Middle();
          if (myDigit === 3)
            return voice.beta3Middle();
          if (myDigit === 4)
            return voice.beta4Middle();
        }
        if (myDigit === 1)
          return voice.beta1Out();
        if (myDigit === 2)
          return voice.beta2Out();
        if (myDigit === 3)
          return voice.beta3Out();
        if (myDigit === 4)
          return voice.beta4Out();
      },
      outputStrings: {
        getOut: {
          en: "Get Out",
          de: "Raus da",
          fr: "Sortez",
          ja: "\u5916\u3078",
          cn: "\u51FA\u53BB",
          ko: "\uBC16\uC73C\uB85C",
          tc: "\u9060\u96E2"
        },
        goIntoMiddle: Voices.goIntoMiddle,
        beta1Middle: Voices.goIntoMiddle,
        beta2Middle: Voices.goIntoMiddle,
        beta3Middle: Voices.goIntoMiddle,
        beta4Middle: Voices.goIntoMiddle,
        beta1Out: {
          en: "Get Out",
          de: "Raus da",
          fr: "Sortez",
          ja: "\u5916\u3078",
          cn: "\u51FA\u53BB",
          ko: "\uBC16\uC73C\uB85C",
          tc: "\u9060\u96E2"
        },
        beta2Out: {
          en: "Get Out",
          de: "Raus da",
          fr: "Sortez",
          ja: "\u5916\u3078",
          cn: "\u51FA\u53BB",
          ko: "\uBC16\uC73C\uB85C",
          tc: "\u9060\u96E2"
        },
        beta3Out: {
          en: "Get Out",
          de: "Raus da",
          fr: "Sortez",
          ja: "\u5916\u3078",
          cn: "\u51FA\u53BB",
          ko: "\uBC16\uC73C\uB85C",
          tc: "\u9060\u96E2"
        },
        beta4Out: {
          en: "Get Out",
          de: "Raus da",
          fr: "Sortez",
          ja: "\u5916\u3078",
          cn: "\u51FA\u53BB",
          ko: "\uBC16\uC73C\uB85C",
          tc: "\u9060\u96E2"
        }
      }
    }),
    raws({
      id: "M12S Blob Tower Followup",
      type: "Ability",
      netRegex: { id: "B4B7", capture: true },
      condition: (pull, hit) => {
        if (pull.myFleshBonds === "alpha" && pull.me === hit.target)
          return true;
        return false;
      },
      infoText: (pull, _hit, voice) => {
        const mechanicDigit = pull.skinsplitterCount;
        const myDigit = pull.inLine[pull.me];
        if (myDigit === void 0)
          return;
        if (myDigit === mechanicDigit)
          return voice.goIntoMiddle();
      },
      outputStrings: {
        goIntoMiddle: Voices.goIntoMiddle
      }
    }),
    raws({
      id: "M12S Skinsplitter Out of Coil Reminder",
      type: "Ability",
      netRegex: { id: "B4BC", capture: false },
      condition: (pull) => pull.skinsplitterCount === 7,
      suppressSeconds: 1,
      alertText: (_pull, _hit, voice) => voice.outOfCoil(),
      outputStrings: {
        outOfCoil: {
          en: "Out of Coil",
          de: "Raus aus dem Kreis",
          cn: "\u51FA\u5708",
          ko: "\uBAB8\uD1B5 \uBC16\uC73C\uB85C"
        }
      }
    }),
    whenChant(["B9C3", "B9C4"]).label("Raidwide").aoe(),
    raws({
      id: "M12S Mitotic Phase Direction Collect",
      type: "GainsEffect",
      netRegex: { effectId: "DE6", capture: true },
      condition: Condition.targetIsYou(),
      durationSeconds: 10,
      infoText: (pull, hit, voice) => {
        pull.myMitoticPhase = hit.count;
        switch (hit.count) {
          case "436":
            return voice.frontTower();
          case "437":
            return voice.rightTower();
          case "438":
            return voice.rearTower();
          case "439":
            return voice.leftTower();
        }
      },
      outputStrings: {
        frontTower: {
          en: "Tower (S/SW)",
          de: "Turm (S/SW)",
          cn: "\u5854 (\u4E0B/\u5DE6\u4E0B)",
          ko: "\uD0D1 (\uB0A8/\uB0A8\uC11C)"
        },
        rearTower: {
          en: "Tower (N/NE)",
          de: "Turm (N/NO)",
          cn: "\u5854 (\u4E0A/\u53F3\u4E0A)",
          ko: "\uD0D1 (\uBD81/\uBD81\uB3D9)"
        },
        leftTower: {
          en: "Tower (E/SE)",
          de: "Turm (O/SO)",
          cn: "\u5854 (\u53F3/\u53F3\u4E0B)",
          ko: "\uD0D1 (\uB3D9/\uB0A8\uB3D9)"
        },
        rightTower: {
          en: "Tower (W/NW)",
          de: "Turm (W/NW)",
          cn: "\u5854 (\u5DE6/\u5DE6\u4E0A)",
          ko: "\uD0D1 (\uC11C/\uBD81\uC11C)"
        }
      }
    }),
    raws({
      id: "M12S Grand Entrance Intercards/Cardinals",
      type: "StartsUsing",
      netRegex: { id: ["B4A1", "B4A2"], capture: true },
      suppressSeconds: 5,
      infoText: (pull, hit, voice) => {
        const tally = pull.myMitoticPhase;
        if (tally === void 0)
          return;
        if (hit.id === "B4A1") {
          switch (tally) {
            case "436":
              return voice.frontCardinals();
            case "437":
              return voice.rightCardinals();
            case "438":
              return voice.rearCardinals();
            case "439":
              return voice.leftCardinals();
          }
        }
        switch (tally) {
          case "436":
            return voice.frontIntercards();
          case "437":
            return voice.rightIntercards();
          case "438":
            return voice.rearIntercards();
          case "439":
            return voice.leftIntercards();
        }
      },
      outputStrings: {
        frontIntercards: Voices.southwest,
        rearIntercards: Voices.northeast,
        leftIntercards: Voices.southeast,
        rightIntercards: Voices.northwest,
        frontCardinals: Voices.south,
        rearCardinals: Voices.north,
        leftCardinals: Voices.east,
        rightCardinals: Voices.west
      }
    }),
    raws({
      id: "M12S Rotting Flesh",
      type: "GainsEffect",
      netRegex: { effectId: "129B", capture: true },
      condition: Condition.targetIsYou(),
      durationSeconds: 10,
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Rotting Flesh on YOU",
          de: "Todeszellen auf DIR",
          cn: "\u81F4\u6B7B\u7EC6\u80DE\u70B9\u540D",
          ko: "\uCE58\uC0AC\uC138\uD3EC \uB300\uC0C1\uC790"
        }
      }
    }),
    raws({
      id: "M12S Rotting Flesh Collect",
      type: "GainsEffect",
      netRegex: { effectId: "129B", capture: true },
      condition: Condition.targetIsYou(),
      run: (pull) => pull.hasRot = true
    }),
    raws({
      id: "M12S Ravenous Reach 2",
      type: "Ability",
      netRegex: { id: ["B49A", "B49B"], source: "Lindwurm", capture: true },
      condition: (pull) => pull.phase === "curtainCall",
      alertText: (pull, hit, voice) => {
        if (hit.id === "B49A") {
          return pull.hasRot ? voice.getHitEast() : voice.safeWest();
        }
        return pull.hasRot ? voice.getHitWest() : voice.safeEast();
      },
      outputStrings: {
        getHitWest: {
          en: "Spread in West Cleave",
          de: "Verteilen im Westen Cleave",
          cn: "\u5DE6\u4FA7\u6247\u5F62\u5185\u5206\u6563",
          ko: "\uC11C\uCABD \uBD80\uCC44\uAF34\uC5D0\uC11C \uC0B0\uAC1C"
        },
        getHitEast: {
          en: "Spread in East Cleave",
          de: "Verteilen im Osten Cleave",
          cn: "\u53F3\u4FA7\u6247\u5F62\u5185\u5206\u6563",
          ko: "\uB3D9\uCABD \uBD80\uCC44\uAF34\uC5D0\uC11C \uC0B0\uAC1C"
        },
        safeEast: {
          en: "Spread East + Avoid Cleave",
          de: "Verteilen im Osten + vermeide Cleave",
          cn: "\u53F3\u4FA7\u5206\u6563 + \u907F\u5F00\u6247\u5F62",
          ko: "\uB3D9\uCABD\uC5D0\uC11C \uC0B0\uAC1C + \uBD80\uCC44\uAF34 \uD53C\uD558\uAE30"
        },
        safeWest: {
          en: "Spread West + Avoid Cleave",
          de: "Verteilen im Westen + vermeide Cleave",
          cn: "\u5DE6\u4FA7\u5206\u6563 + \u907F\u5F00\u6247\u5F62",
          ko: "\uC11C\uCABD\uC5D0\uC11C \uC0B0\uAC1C + \uBD80\uCC44\uAF34 \uD53C\uD558\uAE30"
        }
      }
    }),
    raws({
      id: "M12S Split Scourge and Venomous Scourge",
      type: "ActorControl",
      netRegex: { command: "8000000D", data0: ["1E01", "1E001"], capture: false },
      durationSeconds: 9,
      suppressSeconds: 9999,
      infoText: (pull, _hit, voice) => {
        if (pull.role === "tank")
          return voice.tank();
        return voice.party();
      },
      outputStrings: {
        tank: {
          en: "Bait Line AoE from Heads => Get Middle (Avoid Far AoEs)",
          de: "K\xF6der Linien-AoE von den K\xF6pfen => Geh Mitte (vermeide entfernte AoEs)",
          cn: "\u8BF1\u5BFC\u9F99\u5934\u76F4\u7EBFAoE => \u53BB\u4E2D\u95F4 (\u907F\u5F00\u8FDCAoE)",
          ko: "\uBA38\uB9AC\uC758 \uC9C1\uC120 \uC7A5\uD310 \uC720\uB3C4 => \uC911\uC559\uC73C\uB85C (\uC6D0\uAC70\uB9AC \uC7A5\uD310 \uD53C\uD558\uAE30)"
        },
        party: {
          en: "Away from Heads (Avoid Tank Lines) => Spread near Heads",
          de: "Weg von den K\xF6pfen (Vermeide Tank-Linien) => Verteilen nahe der K\xF6pfe",
          cn: "\u8FDC\u79BB\u5934 (\u907F\u5F00\u5766\u514B\u76F4\u7EBF) => \u9F99\u5934\u9644\u8FD1\u5206\u6563",
          ko: "\uBA38\uB9AC\uC5D0\uC11C \uBA40\uC5B4\uC9C0\uAE30 (\uD0F1\uCEE4 \uC9C1\uC120\uC7A5\uD310 \uD53C\uD558\uAE30) => \uBA38\uB9AC \uADFC\uCC98\uC5D0\uC11C \uC0B0\uAC1C"
        }
      }
    }),
    raws({
      id: "M12S Venomous Scourge",
      type: "Ability",
      netRegex: { id: "B4AB", capture: false },
      durationSeconds: 2.4,
      suppressSeconds: 9999,
      alertText: (pull, _hit, voice) => {
        if (pull.role === "tank")
          return voice.tank();
        return voice.party();
      },
      outputStrings: {
        tank: {
          en: "Get Middle (Avoid Far AoEs)",
          de: "Geh Mittig (Vermeide entfernte AoEs)",
          cn: "\u53BB\u4E2D\u95F4 (\u907F\u5F00\u8FDCAoE)",
          ko: "\uC911\uC559\uC73C\uB85C (\uC6D0\uAC70\uB9AC \uC7A5\uD310 \uD53C\uD558\uAE30)"
        },
        party: {
          en: "Spread near Heads",
          de: "Nahe der K\xF6pfe verteilen",
          cn: "\u9F99\u5934\u9644\u8FD1\u5206\u6563",
          ko: "\uBA38\uB9AC \uADFC\uCC98\uC5D0\uC11C \uC0B0\uAC1C"
        }
      }
    }),
    raws({
      id: "M12S Grotesquerie: Curtain Call Spreads",
      type: "StartsUsing",
      netRegex: { id: "BEC0", source: "Lindwurm", capture: false },
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Bait 5x Puddles",
          de: "K\xF6der Fl\xE4chen x5 ",
          cn: "\u8BF1\u5BFC5\u8F6E\u9EC4\u5708",
          ko: "\uC7A5\uD310 \uC720\uB3C4 5x"
        }
      }
    }),
    raws({
      id: "M12S Curtain Call: Unbreakable Flesh  Alpha Chains",
      type: "GainsEffect",
      netRegex: { effectId: "1291", capture: true },
      condition: (pull, hit) => {
        if (hit.target === pull.me && pull.phase === "curtainCall")
          return true;
        return false;
      },
      infoText: (pull, _hit, voice) => {
        const dir1s = pull.curtainCallSafeCorner;
        const dir2s = dir1s === "northwest" ? "southeast" : "southwest";
        if (dir1s) {
          return voice.alphaChains({
            chains: voice.breakChains(),
            safe: voice.safeSpots({
              dir1: voice[dir1s](),
              dir2: voice[dir2s]()
            })
          });
        }
        return voice.alphaChains({
          chains: voice.breakChains(),
          safe: voice.avoidBlobs()
        });
      },
      outputStrings: {
        northeast: Voices.northeast,
        southeast: Voices.southeast,
        southwest: Voices.southwest,
        northwest: Voices.northwest,
        breakChains: Voices.breakChains,
        safeSpots: {
          en: "${dir1}/${dir2}",
          de: "${dir1}/${dir2}",
          cn: "${dir1}/${dir2}",
          ko: "${dir1}/${dir2}"
        },
        avoidBlobs: {
          en: "Avoid Blobs",
          de: "Vermeide Blobs",
          cn: "\u907F\u5F00\u5371\u9669\u533A\u57DF",
          ko: "\uC0B4\uC810 \uD53C\uD558\uAE30"
        },
        alphaChains: {
          en: "${chains} => ${safe}",
          de: "${chains} => ${safe}",
          cn: "${chains} => ${safe}",
          ko: "${chains} => ${safe}"
        }
      }
    }),
    raws({
      id: "M12S Curtain Call Safe Spot",
      type: "LosesEffect",
      netRegex: { effectId: "1291", capture: true },
      condition: (pull, hit) => {
        if (hit.target === pull.me && pull.phase === "curtainCall")
          return true;
        return false;
      },
      promise: async (pull) => {
        if (pull.triggerSetConfig.curtainCallStrat !== "ns")
          return;
        const dir1s = pull.curtainCallSafeCorner;
        const dir2s = dir1s === "northwest" ? "southeast" : "southwest";
        const combatantTwo = (await sayOverlayHandler({
          call: "getCombatants",
          names: [pull.me]
        })).combatants;
        const meBit = combatantTwo[0];
        if (combatantTwo.length !== 1 || meBit === void 0) {
          consol.error(
            `M12S Curtain Call Safe Spot: Wrong combatants count ${combatantTwo.length}`
          );
          return;
        }
        const x = meBit.PosX;
        const y = meBit.PosY;
        if (y > 100) {
          if (x < 100)
            pull.myCurtainCallSafeSpot = dir1s === "northeast" ? dir2s : dir1s;
          else if (x >= 100)
            pull.myCurtainCallSafeSpot = dir1s === "northeast" ? dir1s : dir2s;
        } else if (y <= 100) {
          pull.myCurtainCallSafeSpot = dir1s;
        }
      },
      alertText: (pull, _hit, voice) => {
        if (pull.triggerSetConfig.curtainCallStrat === "none") {
          const dir1s = pull.curtainCallSafeCorner;
          const dir2s = dir1s === "northwest" ? "southeast" : "southwest";
          if (dir1s === void 0)
            return voice.avoidBlobs();
          return voice.safeSpots({
            dir1: voice[dir1s](),
            dir2: voice[dir2s]()
          });
        }
        const myCurtainSayClearSpot = pull.myCurtainCallSafeSpot;
        if (myCurtainSayClearSpot === void 0)
          return voice.avoidBlobs();
        return voice[myCurtainSayClearSpot]();
      },
      outputStrings: {
        northeast: Voices.northeast,
        southeast: Voices.southeast,
        southwest: Voices.southwest,
        northwest: Voices.northwest,
        avoidBlobs: {
          en: "Avoid Blobs",
          de: "Vermeide Blobs",
          cn: "\u907F\u5F00\u5371\u9669\u533A\u57DF",
          ko: "\uC0B4\uC810 \uD53C\uD558\uAE30"
        },
        safeSpots: {
          en: "${dir1}/${dir2}",
          de: "${dir1}/${dir2}",
          cn: "${dir1}/${dir2}",
          ko: "${dir1}/${dir2}"
        }
      }
    }),
    whenChant(["B4C6", "B4C3"]).label("Big Raidwide (B4C6)").alert("Big Raidwide"),
    raws({
      id: "M12S Slaughtershed Actor Probe 0",
      type: "StartsUsing",
      netRegex: { id: ["B4C6", "B4C3"], capture: false },
      delaySeconds: 0.1,
      suppressSeconds: 30,
      run: (pull) => m12sDumpBodies("slaughter+0")
    }),
    raws({
      id: "M12S Slaughtershed Actor Probe 1",
      type: "StartsUsing",
      netRegex: { id: ["B4C6", "B4C3"], capture: false },
      delaySeconds: 2.5,
      suppressSeconds: 30,
      run: (pull) => m12sDumpBodies("slaughter+2.5")
    }),
    raws({
      id: "M12S Slaughershed Stack/Spread Spots (Early)",
      type: "Ability",
      netRegex: { id: ["B4C6", "B4C3"], source: "Lindwurm", capture: false },
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const slaughtersheds = pull.splattershedStackDir;
        if (slaughtersheds === void 0)
          return;
        return voice[slaughtersheds]();
      },
      outputStrings: {
        northeast: {
          en: "Stack NE/Spread NW (later)",
          de: "Sammel NO/Verteilen NW (Sp\xE4ter)",
          cn: "\u53F3\u4E0A\u5206\u644A/\u5DE6\u4E0A\u5206\u6563 (\u7A0D\u540E)",
          ko: "\uC250\uC5B4 \uBD81\uB3D9\uCABD/\uC0B0\uAC1C \uBD81\uC11C\uCABD (\uB098\uC911\uC5D0)"
        },
        northwest: {
          en: "Spread NE/Stack NW (later)",
          de: "Verteilen NO/Sammel NW (Sp\xE4ter)",
          cn: "\u53F3\u4E0A\u5206\u6563/\u5DE6\u4E0A\u5206\u644A (\u7A0D\u540E)",
          ko: "\uC0B0\uAC1C \uBD81\uB3D9\uCABD/\uC250\uC5B4 \uBD81\uC11C\uCABD (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M12S Serpintine Scourge/Raptor Knuckles Collect",
      type: "Ability",
      netRegex: {
        id: ["B4CB", "B4CD", "B4CC", "B4CE"],
        source: "Lindwurm",
        capture: true
      },
      condition: (pull) => pull.phase === "slaughtershed",
      run: (pull, hit) => {
        switch (hit.id) {
          case "B4CB":
            pull.slaughtershed = "right";
            break;
          case "B4CD":
            pull.slaughtershed = "left";
            break;
          case "B4CC":
            pull.slaughtershed = "northwestKnockback";
            break;
          case "B4CE":
            pull.slaughtershed = "northeastKnockback";
        }
      }
    }),
    raws({
      id: "M12S Slaughtershed Stack",
      type: "HeadMarker",
      netRegex: { id: headSignState["slaughterStack"], capture: true },
      condition: (pull, hit) => {
        const isDPSBit = pull.party.isDPS(hit.target);
        if (isDPSBit && pull.role === "dps")
          return true;
        if (!isDPSBit && pull.role !== "dps")
          return true;
        return false;
      },
      delaySeconds: 0.1,
      durationSeconds: 6,
      alertText: (pull, hit, voice) => {
        const facingTwo = pull.splattershedStackDir;
        const markThree = hit.target;
        const slaughters = pull.slaughtershed;
        if (markThree === pull.me) {
          if (facingTwo) {
            if (slaughters === void 0)
              return voice.stackDir({
                stack: voice.stackOnYou(),
                dir: voice[facingTwo]()
              });
            return voice.stackThenDodge({
              stack: voice.stackDir({
                stack: voice.stackOnYou(),
                dir: voice[facingTwo]()
              }),
              dodge: voice[slaughters]()
            });
          }
          if (slaughters === void 0)
            return voice.stackOnYou();
          return voice.stackThenDodge({
            stack: voice.stackOnYou(),
            dodge: voice[slaughters]()
          });
        }
        const player = pull.party.member(markThree);
        if (facingTwo) {
          if (slaughters === void 0)
            return voice.stackDir({
              stack: voice.stackOnPlayer({ player }),
              dir: voice[facingTwo]()
            });
          return voice.stackThenDodge({
            stack: voice.stackDir({
              stack: voice.stackOnPlayer({ player }),
              dir: voice[facingTwo]()
            }),
            dodge: voice[slaughters]()
          });
        }
        if (slaughters === void 0)
          return voice.stackOnPlayer({ player });
        return voice.stackThenDodge({
          stack: voice.stackOnPlayer({ player }),
          dodge: voice[slaughters]()
        });
      },
      outputStrings: {
        left: Voices.left,
        right: Voices.right,
        northeastKnockback: {
          en: "Knockback from Northeast",
          de: "R\xFCcksto\xDF von Nordosten",
          cn: "\u4ECE\u53F3\u4E0A\u51FB\u9000",
          ko: "\uBD81\uB3D9\uCABD\uC5D0\uC11C \uB109\uBC31"
        },
        northwestKnockback: {
          en: "Knockback from Northwest",
          de: "R\xFCcksto\xDF von Nordwesten",
          cn: "\u4ECE\u5DE6\u4E0A\u51FB\u9000",
          ko: "\uBD81\uC11C\uCABD\uC5D0\uC11C \uB109\uBC31"
        },
        northeast: Voices.dirNE,
        northwest: Voices.dirNW,
        stackOnYou: Voices.stackOnYou,
        stackOnPlayer: Voices.stackOnPlayer,
        stackDir: {
          en: "${stack} ${dir}",
          de: "${stack} ${dir}",
          cn: "${stack} ${dir}",
          ko: "${stack} ${dir}"
        },
        stackThenDodge: {
          en: "${stack} => ${dodge}",
          de: "${stack} => ${dodge}",
          cn: "${stack} => ${dodge}",
          ko: "${stack} => ${dodge}"
        }
      }
    }),
    raws({
      id: "M12S Slaughtershed Spread",
      type: "HeadMarker",
      netRegex: { id: headSignState["slaughterSpread"], capture: true },
      condition: Condition.targetIsYou(),
      delaySeconds: 0.1,
      durationSeconds: 6,
      suppressSeconds: 1,
      alertText: (pull, _hit, voice) => {
        const pileFacing = pull.splattershedStackDir;
        const facingTwo = pileFacing === "northwest" ? "northeast" : pileFacing === "northeast" ? "northwest" : void 0;
        const slaughters = pull.slaughtershed;
        if (facingTwo) {
          if (slaughters === void 0)
            return voice.spreadDir({ dir: voice[facingTwo]() });
          return voice.spreadThenDodge({
            spread: voice.spreadDir({ dir: voice[facingTwo]() }),
            dodge: voice[slaughters]()
          });
        }
        if (slaughters === void 0)
          return voice.spread();
        return voice.spreadThenDodge({
          spread: voice.spread(),
          dodge: voice[slaughters]()
        });
      },
      outputStrings: {
        left: Voices.left,
        right: Voices.right,
        northeastKnockback: {
          en: "Knockback from Northeast",
          de: "R\xFCcksto\xDF von Nordosten",
          cn: "\u4ECE\u53F3\u4E0A\u51FB\u9000",
          ko: "\uBD81\uB3D9\uCABD\uC5D0\uC11C \uB109\uBC31"
        },
        northwestKnockback: {
          en: "Knockback from Northwest",
          de: "R\xFCcksto\xDF von Nordwesten",
          cn: "\u4ECE\u5DE6\u4E0A\u51FB\u9000",
          ko: "\uBD81\uC11C\uCABD\uC5D0\uC11C \uB109\uBC31"
        },
        northeast: Voices.dirNE,
        northwest: Voices.dirNW,
        spread: Voices.spread,
        spreadDir: {
          en: "Spread ${dir}",
          de: "Verteilen ${dir}",
          cn: "${dir}\u5206\u6563",
          ko: "\uC0B0\uAC1C ${dir}"
        },
        spreadThenDodge: {
          en: "${spread} => ${dodge}",
          de: "${spread} => ${dodge}",
          cn: "${spread} => ${dodge}",
          ko: "${spread} => ${dodge}"
        }
      }
    }),
    raws({
      id: "M12S Splattershed Safe Spot Cleanup",
      type: "HeadMarker",
      netRegex: { id: headSignState["slaughterStack"], capture: false },
      delaySeconds: 0.2,
      run: (pull) => delete pull.splattershedStackDir
    }),
    raws({
      id: "M12S Serpintine Scourge and Raptor Knuckles",
      type: "Ability",
      netRegex: { id: ["B4D4", "B4D5"], source: "Lindwurm", capture: false },
      durationSeconds: 5.5,
      suppressSeconds: 1,
      alertText: (pull, _hit, voice) => {
        const slaughtersheds = pull.slaughtershed;
        if (slaughtersheds)
          return voice[slaughtersheds]();
      },
      outputStrings: {
        right: Voices.rightThenLeft,
        left: Voices.leftThenRight,
        northwestKnockback: {
          en: "Knockback from Northwest => Knockback from Northeast",
          de: "R\xFCcksto\xDF von Nordwesten => R\xFCcksto\xDF von Nordosten",
          cn: "\u4ECE\u5DE6\u4E0A\u51FB\u9000 => \u4ECE\u53F3\u4E0A\u51FB\u9000",
          ko: "\uBD81\uC11C\uC5D0\uC11C \uB109\uBC31 => \uBD81\uB3D9\uC5D0\uC11C \uB109\uBC31"
        },
        northeastKnockback: {
          en: "Knockback from Northeast => Knockback from Northwest",
          de: "R\xFCcksto\xDF von Nordosten => R\xFCcksto\xDF von Nordwesten",
          cn: "\u4ECE\u53F3\u4E0A\u51FB\u9000 => \u4ECE\u5DE6\u4E0A\u51FB\u9000",
          ko: "\uBD81\uB3D9\uC5D0\uC11C \uB109\uBC31 => \uBD81\uC11C\uC5D0\uC11C \uB109\uBC31"
        }
      }
    }),
    raws({
      id: "M12S Raptor Knuckles Uptime Knockback",
      type: "Ability",
      netRegex: { id: ["B4CC", "B4CE"], source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.phase === "slaughtershed" && pull.triggerSetConfig.uptimeKnockbackStrat)
          return true;
        return false;
      },
      delaySeconds: 11.5,
      durationSeconds: 1.8,
      response: Response.knockback()
    }),
    raws({
      id: "M12S Serpentine Scourge Left Followup",
      type: "Ability",
      netRegex: { id: "B4D1", source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.slaughtershed)
          return true;
        return false;
      },
      response: Response.goLeft()
    }),
    raws({
      id: "M12S Serpentine Scourge Right Followup",
      type: "Ability",
      netRegex: { id: "B4D2", source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.slaughtershed)
          return true;
        return false;
      },
      response: Response.goRight()
    }),
    raws({
      id: "M12S Raptor Knuckles Northeast Followup",
      type: "Ability",
      netRegex: { id: "B4D0", source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.slaughtershed)
          return true;
        return false;
      },
      delaySeconds: 0.8,
      alertText: (_pull, _hit, voice) => voice.northwestKnockback(),
      outputStrings: {
        northwestKnockback: {
          en: "Knockback from Northwest",
          de: "R\xFCcksto\xDF von Nordwesten",
          cn: "\u4ECE\u5DE6\u4E0A\u51FB\u9000",
          ko: "\uBD81\uC11C\uCABD\uC5D0\uC11C \uB109\uBC31"
        }
      }
    }),
    raws({
      id: "M12S Raptor Knuckles Northwest Followup",
      type: "Ability",
      netRegex: { id: "B4CF", source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.slaughtershed)
          return true;
        return false;
      },
      delaySeconds: 0.8,
      alertText: (_pull, _hit, voice) => voice.northeastKnockback(),
      outputStrings: {
        northeastKnockback: {
          en: "Knockback from Northeast",
          de: "R\xFCcksto\xDF von Nordosten",
          cn: "\u4ECE\u53F3\u4E0A\u51FB\u9000",
          ko: "\uBD81\uB3D9\uCABD\uC5D0\uC11C \uB109\uBC31"
        }
      }
    }),
    raws({
      id: "M12S Slaughtershed Cleanup",
      type: "Ability",
      netRegex: { id: ["B4D1", "B4D2", "B4D0", "B4CF"], source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.slaughtershed)
          return true;
        return false;
      },
      run: (pull) => delete pull.slaughtershed
    }),
    whenSkill("B539").label("Big Raidwide (B539)").alert("Big Raidwide").hold(5).cooldown(9999),
  ],
});
