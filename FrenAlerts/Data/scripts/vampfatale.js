
const headSignState = {
  "multiHitStack": "0131",
  "tankbuster": "01D4",
  "aetherletting": "028C",
  "tetherClose": "0161",
  "tetherFar": "0162"
};
const centers = {
  x: 100,
  y: 100
};

function m9sShoveCoffinFiller(pull, xPlace) {
  if (!Number.isFinite(xPlace))
    return;
  let dangers;
  if (xPlace < 95)
    dangers = "outerWest";
  else if (xPlace < 100)
    dangers = "innerWest";
  else if (xPlace < 105)
    dangers = "innerEast";
  else
    dangers = "outerEast";
  pull.coffinPhase = true;
  pull.coffinfillers.push(dangers);
}

function m9sHalfMoonAlerts(pull, hit, voice) {
  if (pull.coffinfillers.length < 2) {
    if (pull.coffinPhase)
      return;
    if (hit.id === "B377")
      return voice.rightThenLeft();
    if (hit.id === "B37B")
      return voice.leftThenRight();
    return voice.bigHalfmoonNoCoffin({
      dir1: voice[hit.id === "B379" ? "right" : "left"](),
      dir2: voice[hit.id === "B379" ? "left" : "right"]()
    });
  }
  const attackFacingDigit = Facings.hdgTo4DirNum(parseFloat(hit.heading));
  const facingNum1 = (attackFacingDigit + 2) % 4;
  const dir1s = Facings.outputFromCardinalNum(facingNum1);
  const facingNum2 = attackFacingDigit;
  const dir2s = Facings.outputFromCardinalNum(facingNum2);
  const bigSweep = hit.id === "B379" || hit.id === "B37D";
  const insidePlaces = [
    "innerWest",
    "innerEast"
  ];
  const outsidePlaces = [
    "outerWest",
    "outerEast"
  ];
  const westPlaces = [
    "innerWest",
    "outerWest"
  ];
  const eastPlaces = [
    "innerEast",
    "outerEast"
  ];
  let coffinSafe1s = [
    "outerWest",
    "innerWest",
    "innerEast",
    "outerEast"
  ];
  coffinSafe1s = coffinSafe1s.filter((place) => !pull.coffinfillers.includes(place));
  let coffinSafe2s = [
    "outerWest",
    "innerWest",
    "innerEast",
    "outerEast"
  ];
  coffinSafe2s = coffinSafe2s.filter((place) => pull.coffinfillers.includes(place));
  pull.coffinfillers = [];
  pull.pendingHalfMoon = null;
  let dir1Line = voice[dir1s]();
  let dir2Line = voice[dir2s]();
  if (dir1s === "dirW") {
    coffinSafe1s = coffinSafe1s.filter((place) => westPlaces.includes(place));
    dir1Line = voice.leftWest();
  }
  if (dir1s === "dirE") {
    coffinSafe1s = coffinSafe1s.filter((place) => eastPlaces.includes(place));
    dir1Line = voice.rightEast();
  }
  if (dir2s === "dirW") {
    coffinSafe2s = coffinSafe2s.filter((place) => westPlaces.includes(place));
    dir2Line = voice.leftWest();
  }
  if (dir2s === "dirE") {
    coffinSafe2s = coffinSafe2s.filter((place) => eastPlaces.includes(place));
    dir2Line = voice.rightEast();
  }
  let coffin1s;
  let coffin2s;
  if (coffinSafe1s.every((place) => insidePlaces.includes(place)))
    coffin1s = "inside";
  else if (coffinSafe1s.every((place) => outsidePlaces.includes(place)))
    coffin1s = "outside";
  else
    coffin1s = coffinSafe1s.find((place) => insidePlaces.includes(place)) ?? "unknown";
  if (coffinSafe2s.every((place) => insidePlaces.includes(place)))
    coffin2s = "inside";
  else if (coffinSafe2s.every((place) => outsidePlaces.includes(place)))
    coffin2s = "outside";
  else
    coffin2s = coffinSafe2s.find((place) => insidePlaces.includes(place)) ?? "unknown";
  if (bigSweep) {
    return voice.bigHalfmoonCombined({
      coffin1: voice[coffin1s](),
      dir1: dir1Line,
      coffin2: voice[coffin2s](),
      dir2: dir2Line
    });
  }
  return voice.combined({
    coffin1: voice[coffin1s](),
    dir1: dir1Line,
    coffin2: voice[coffin2s](),
    dir2: dir2Line
  });
}

defineDuty({
  id: "AacHeavyweightM1Savage",
  name: "M9S - Vamp Fatale",
  category: "Savage \u2013 Dawntrail",
  zoneId: 1321,
  boss: "Vamp Fatale",
  center: { x: 100, y: 100 },
  state: {
    flailPositions: [],
    coffinfillers: [],
    coffinPhase: false,
    pendingHalfMoon: null,
    actorPositions: {},
    bats: { inner: [], middle: [], outer: [] },
    satisfiedCount: 0,
    hasHellAwaits: false
  },
  mechanics: [
    raws({
      id: "M9S ActorSetPos Tracker",
      type: "ActorSetPos",
      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      run: (pull, hit) => pull.actorPositions[hit.id] = {
        x: parseFloat(hit.x),
        y: parseFloat(hit.y),
        heading: parseFloat(hit.heading)
      }
    }),
    raws({
      id: "M9S ActorMove Tracker",
      type: "ActorMove",
      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      run: (pull, hit) => pull.actorPositions[hit.id] = {
        x: parseFloat(hit.x),
        y: parseFloat(hit.y),
        heading: parseFloat(hit.heading)
      }
    }),
    raws({
      id: "M9S AddedCombatant Tracker",
      type: "AddedCombatant",
      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      run: (pull, hit) => pull.actorPositions[hit.id] = {
        x: parseFloat(hit.x),
        y: parseFloat(hit.y),
        heading: parseFloat(hit.heading)
      }
    }),
    whenChant("B384").label("Raidwide (B384)").aoe(),
    raws({
      id: "M9S Satisfied Counter",
      type: "GainsEffect",
      netRegex: { effectId: "1277", capture: true },
      run: (pull, hit) => pull.satisfiedCount = parseInt(hit.count)
    }),
    raws({
      id: "M9S Headmarker Tankbuster",
      type: "HeadMarker",
      netRegex: { id: headSignState["tankbuster"], capture: true },
      condition: Condition.targetIsYou(),
      alertText: (pull, _hit, voice) => {
        if (pull.satisfiedCount >= 8)
          return voice.bigTankCleave();
        return voice.tankCleaveOnYou();
      },
      outputStrings: {
        tankCleaveOnYou: Voices.tankCleaveOnYou,
        bigTankCleave: {
          en: "Tank Cleave on YOU (Big)",
          de: "Tank Cleave auf DIR (Gro\xDF)",
          fr: "Tank cleave sur VOUS (Gros)",
          ja: "\u81EA\u5206\u306B\u30BF\u30F3\u30AF\u7BC4\u56F2\u653B\u6483\uFF08\u5927\uFF09",
          cn: "\u5766\u514B\u8303\u56F4\u6B7B\u5211\u70B9\u540D\uFF08\u5927\uFF09",
          ko: "\uAD11\uC5ED \uD0F1\uBC84 \uB300\uC0C1\uC790 (\uD070)"
        }
      }
    }),
    whenChant("B34A").label("Out").out(),
    raws({
      id: "M9S Headmarker Party Multi Stack",
      type: "HeadMarker",
      netRegex: { id: headSignState["multiHitStack"], capture: true },
      response: Response.stackMarkerOn()
    }),
    raws({
      id: "M9S Bat Tracker",
      type: "ActorControlExtra",
      netRegex: { id: "4[0-9A-Fa-f]{7}", category: "0197", param1: "11D1", capture: true },
      run: (pull, hit) => {
        const moveRad = {
          "inner": 1.5128,
          "middle": 1.5513,
          "outer": 1.5608
        };
        const actor = pull.actorPositions[hit.id];
        if (actor === void 0)
          return;
        const dists = Math.hypot(actor.x - centers.x, actor.y - centers.y);
        const dLens = dists < 16 ? dists < 8 ? "inner" : "middle" : "outer";
        const turnAmount = Math.atan2(actor.x - centers.x, actor.y - centers.y);
        let turnAmountCW = turnAmount - Math.PI / 2;
        if (turnAmountCW < -Math.PI)
          turnAmountCW += Math.PI * 2;
        let turnAmountDiff = Math.abs(turnAmountCW - actor.heading);
        if (turnAmountDiff > Math.PI * 1.75)
          turnAmountDiff = Math.abs(turnAmountDiff - Math.PI * 2);
        const cwBit = turnAmountDiff < Math.PI / 2 ? "cw" : "ccw";
        const adjustRad = moveRad[dLens];
        let closeTurnAmount = turnAmount + adjustRad * (cwBit === "cw" ? -1 : 1);
        if (closeTurnAmount < -Math.PI)
          closeTurnAmount += Math.PI * 2;
        else if (closeTurnAmount > Math.PI)
          closeTurnAmount -= Math.PI * 2;
        pull.bats[dLens].push(
          Facings.output16Dir[Facings.hdgTo16DirNum(closeTurnAmount)] ?? "unknown"
        );
      }
    }),
    raws({
      id: "M9S Blast Beat Inner",
      type: "ActorControlExtra",
      netRegex: { id: "4[0-9A-Fa-f]{7}", category: "0197", param1: "11D1", capture: false },
      delaySeconds: 4.1,
      durationSeconds: 5.5,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const [dir1s, dir2s] = pull.bats.inner.sort(Facings.compareDirectionOutput);
        return voice.away({
          dir1: voice[dir1s ?? "unknown"](),
          dir2: voice[dir2s ?? "unknown"]()
        });
      },
      run: (pull, _hit) => {
        pull.bats.inner = [];
      },
      outputStrings: {
        ...Facings.outputStrings16Dir,
        away: {
          en: "Away from bats ${dir1}/${dir2}",
          de: "Weg von den Flederm\xE4usen ${dir1}/${dir2}",
          fr: "Loin des chauves-souris ${dir1}/${dir2}",
          ja: "\u30B3\u30A6\u30E2\u30EA\u304B\u3089\u96E2\u308C\u308B ${dir1}/${dir2}",
          cn: "\u8FDC\u79BB ${dir1}\u3001${dir2} \u8759\u8760",
          ko: "\uBC15\uC950 \uD53C\uD558\uAE30 ${dir1}/${dir2}"
        }
      }
    }),
    raws({
      id: "M9S Blast Beat Middle",
      type: "ActorControlExtra",
      netRegex: { id: "4[0-9A-Fa-f]{7}", category: "0197", param1: "11D1", capture: false },
      delaySeconds: 9.7,
      durationSeconds: 3.4,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const [dir1s, dir2s, dir3s] = pull.bats.middle.sort(Facings.compareDirectionOutput);
        return voice.away({
          dir1: voice[dir1s ?? "unknown"](),
          dir2: voice[dir2s ?? "unknown"](),
          dir3: voice[dir3s ?? "unknown"]()
        });
      },
      run: (pull, _hit) => {
        pull.bats.middle = [];
      },
      outputStrings: {
        ...Facings.outputStrings16Dir,
        away: {
          en: "Away from bats ${dir1}/${dir2}/${dir3}",
          de: "Weg von den Flederm\xE4usen ${dir1}/${dir2}/${dir3}",
          fr: "Loin des chauves-souris ${dir1}/${dir2}/${dir3}",
          ja: "\u30B3\u30A6\u30E2\u30EA\u304B\u3089\u96E2\u308C\u308B ${dir1}/${dir2}/${dir3}",
          cn: "\u8FDC\u79BB ${dir1}\u3001${dir2}\u3001${dir3} \u8759\u8760",
          ko: "\uBC15\uC950 \uD53C\uD558\uAE30 ${dir1}/${dir2}/${dir3}"
        }
      }
    }),
    raws({
      id: "M9S Blast Beat Outer",
      type: "ActorControlExtra",
      netRegex: { id: "4[0-9A-Fa-f]{7}", category: "0197", param1: "11D1", capture: false },
      delaySeconds: 13.2,
      durationSeconds: 3.4,
      suppressSeconds: 1,
      response: Response.goMiddle(),
      run: (pull, _hit) => {
        pull.bats.outer = [];
      }
    }),
    raws({
      id: "M9S Sadistic Screech",
      type: "StartsUsing",
      netRegex: { id: "B333", source: "Vamp Fatale", capture: false },
      response: Response.bigAoe(),
      run: (pull) => {
        pull.coffinfillers = [];
      }
    }),
    raws({
      id: "M9S Sadistic Screech Arena",
      type: "Ability",
      netRegex: { id: "B366", capture: false },
      run: (pull) => {
        pull.coffinfillers = [];
        pull.pendingHalfMoon = null;
        pull.coffinPhase = !pull.coffinPhase;
      }
    }),
    raws({
      id: "M9S Coffinfiller",
      type: "StartsUsingExtra",
      netRegex: { id: ["B368", "B369", "B36A"], capture: true },
      preRun: (pull, hit) => m9sShoveCoffinFiller(pull, parseFloat(hit.x)),
      alertText: (pull, _hit, voice) => {
        if (!pull.coffinPhase || !pull.pendingHalfMoon || pull.coffinfillers.length < 2)
          return;
        return m9sHalfMoonAlerts(pull, pull.pendingHalfMoon, voice);
      }
    }),
    raws({
      id: "M9S Coffinfiller Ability",
      type: "AbilityExtra",
      netRegex: { id: ["B368", "B369", "B36A"], capture: true },
      preRun: (pull, hit) => m9sShoveCoffinFiller(pull, parseFloat(hit.x)),
      alertText: (pull, _hit, voice) => {
        if (!pull.coffinPhase || !pull.pendingHalfMoon || pull.coffinfillers.length < 2)
          return;
        return m9sHalfMoonAlerts(pull, pull.pendingHalfMoon, voice);
      }
    }),
    raws({
      id: "M9S Half Moon",
      type: "StartsUsingExtra",
      netRegex: { id: ["B377", "B379", "B37B", "B37D"], capture: true },
      delaySeconds: (pull) => pull.coffinPhase ? 0 : 0.3,
      alertText: (pull, hit, voice) => {
        if (pull.coffinPhase) {
          pull.pendingHalfMoon = hit;
          if (pull.coffinfillers.length < 2)
            return;
        }
        return m9sHalfMoonAlerts(pull, hit, voice);
      },
      outputStrings: {
        ...Facings.outputStringsCardinalDir,
        text: {
          en: "${first} => ${second}",
          de: "${first} => ${second}",
          fr: "${first} => ${second}",
          ja: "${first} => ${second}",
          cn: "${first} => ${second}",
          ko: "${first} => ${second}"
        },
        combined: {
          en: "${coffin1} + ${dir1} => ${coffin2} + ${dir2}",
          de: "${coffin1} + ${dir1} => ${coffin2} + ${dir2}",
          fr: "${coffin1} + ${dir1} => ${coffin2} + ${dir2}",
          ja: "${coffin1} + ${dir1} => ${coffin2} + ${dir2}",
          cn: "${coffin1} + ${dir1} => ${coffin2} + ${dir2}",
          ko: "${coffin1} + ${dir1} => ${coffin2} + ${dir2}"
        },
        bigHalfmoonCombined: {
          en: "${coffin1} + ${dir1} (big) => ${coffin2} + ${dir2} (big)",
          de: "${coffin1} + ${dir1} (gro\xDF) => ${coffin2} + ${dir2} (gro\xDF)",
          fr: "${coffin1} + ${dir1} (big) => ${coffin2} + ${dir2} (gros)",
          ja: "${coffin1} + ${dir1} (\u5927) => ${coffin2} + ${dir2} (\u5927)",
          cn: "${coffin1} + ${dir1} (\u5927) => ${coffin2} + ${dir2} (\u5927)",
          ko: "${coffin1} + ${dir1} (\uD070) => ${coffin2} + ${dir2} (\uD070)"
        },
        rightThenLeft: Voices.rightThenLeft,
        leftThenRight: Voices.leftThenRight,
        left: Voices.left,
        leftWest: Voices.leftWest,
        right: Voices.right,
        rightEast: Voices.rightEast,
        inside: {
          en: "Inside",
          de: "Innen",
          fr: "Int\xE9rieur",
          ja: "\u5185\u5074",
          cn: "\u5185\u4FA7",
          ko: "\uC548\uCABD"
        },
        outside: {
          en: "Outside",
          de: "Au\xDFen",
          fr: "Ext\xE9rieur",
          ja: "\u5916\u5074",
          cn: "\u5916\u4FA7",
          ko: "\uBC14\uAE65\uCABD"
        },
        outerWest: {
          en: "Outer West",
          de: "Au\xDFen Westen",
          fr: "Ext\xE9rieur Ouest",
          ja: "\u5DE6\u5916",
          cn: "\u5DE6\u5916",
          ko: "\uBC14\uAE65 \uC11C\uCABD"
        },
        innerWest: {
          en: "Inner West",
          de: "Innen Westen",
          fr: "Int\xE9rieur Ouest",
          ja: "\u5DE6\u5185",
          cn: "\u5DE6\u5185",
          ko: "\uC548 \uC11C\uCABD"
        },
        innerEast: {
          en: "Inner East",
          de: "Innen Osten",
          fr: "Int\xE9rieur Est",
          ja: "\u53F3\u5185",
          cn: "\u53F3\u5185",
          ko: "\uC548 \uB3D9\uCABD"
        },
        outerEast: {
          en: "Outer East",
          de: "Au\xDFen Osten",
          fr: "Ext\xE9rieur Est",
          ja: "\u53F3\u5916",
          cn: "\u53F3\u5916",
          ko: "\uBC14\uAE65 \uB3D9\uCABD"
        },
        bigHalfmoonNoCoffin: {
          en: "${dir1} max melee => ${dir2} max melee",
          de: "${dir1} max Nahk\xE4mpfer => ${dir2} max Nahk\xE4mpfer",
          fr: "${dir1} max mel\xE9e => ${dir2} max mel\xE9e",
          ja: "${dir1} \u30E1\u30EC\u30FC\u6700\u5927\u8DDD\u96E2 => ${dir2} \u30E1\u30EC\u30FC\u6700\u5927\u8DDD\u96E2",
          cn: "${dir1} \u6700\u5927\u8FD1\u6218\u8DDD\u79BB => ${dir2} \u6700\u5927\u8FD1\u6218\u8DDD\u79BB",
          ko: "${dir1} \uCE7C\uB05D\uB51C => ${dir2} \uCE7C\uB05D\uB51C"
        }
      }
    }),
    whenChant("B33E").label("Big Raidwide (B33E)").bigAoe(),
    whenChant("B344").label("Big Raidwide (B344)").bigAoe(),
    whenChant("B340").label("Raidwide (B340)").aoe(),
    whenChant("B341").label("Big Raidwide (B341)").bigAoe(),
    whenSign("028C").label("Aetherletting").onYou().info("Aetherletting on YOU"),
    raws({
      id: "M9S Plummet",
      type: "StartsUsingExtra",
      netRegex: { id: "B38B", capture: true },
      preRun: (pull, hit) => {
        pull.flailPositions.push(hit);
      },
      infoText: (pull, _hit, voice) => {
        const [flail1Hit, flail2Hit] = pull.flailPositions;
        if (flail1Hit === void 0 || flail2Hit === void 0)
          return;
        const flail1XBit = parseFloat(flail1Hit.x);
        const flail1YBit = parseFloat(flail1Hit.y);
        const flail2XBit = parseFloat(flail2Hit.x);
        const flail2YBit = parseFloat(flail2Hit.y);
        const flail1Facing = Facings.xyToIntercardDirOutput(flail1XBit, flail1YBit, centers.x, centers.y);
        const flail2Facing = Facings.xyToIntercardDirOutput(flail2XBit, flail2YBit, centers.x, centers.y);
        const flail1Dists = Math.abs(flail1YBit - centers.y) < 10 ? "near" : "far";
        const flail2Dists = Math.abs(flail1YBit - centers.y) < 10 ? "near" : "far";
        return voice.text({
          flail1Dir: voice[flail1Facing](),
          flail2Dir: voice[flail2Facing](),
          flail1Dist: voice[flail1Dists](),
          flail2Dist: voice[flail2Dists]()
        });
      },
      run: (pull) => {
        if (pull.flailPositions.length < 2)
          return;
        pull.flailPositions = [];
      },
      outputStrings: {
        text: {
          en: "Flails ${flail1Dist} ${flail1Dir}/${flail2Dist} ${flail2Dir}",
          de: "Stachelbombe ${flail1Dist} ${flail1Dir}/${flail2Dist} ${flail2Dir}",
          fr: "Fl\xE9aux ${flail1Dist} ${flail1Dir}/${flail2Dist} ${flail2Dir}",
          ja: "\u30D5\u30EC\u30A4\u30EB ${flail1Dist}${flail1Dir}\u3001${flail2Dist}${flail2Dir}",
          cn: "\u523A\u9524 ${flail1Dist}${flail1Dir}\u3001${flail2Dist}${flail2Dir}",
          ko: "\uCCA0\uD1F4 ${flail1Dist} ${flail1Dir}/${flail2Dist} ${flail2Dir}"
        },
        near: {
          en: "Near",
          de: "Nah",
          fr: "proche",
          ja: "\u8FD1\u304F",
          cn: "\u8FD1",
          ko: "\uAC00\uAE4C\uC774",
          tc: "\u8FD1"
        },
        far: {
          en: "Far",
          de: "Fern",
          fr: "loin",
          ja: "\u9060\u304F",
          cn: "\u8FDC",
          ko: "\uBA40\uB9AC",
          tc: "\u9060"
        },
        ...Facings.outputStringsIntercardDir
      }
    }),
    raws({
      id: "M9S Hell Awaits Gain Debuff Collector",
      type: "GainsEffect",
      netRegex: { effectId: "127A", capture: true },
      condition: Condition.targetIsYou(),
      run: (pull) => {
        pull.hasHellAwaits = true;
      }
    }),
    raws({
      id: "M9S Hell Awaits Lose Debuff Collector",
      type: "GainsEffect",
      netRegex: { effectId: "127A", capture: true },
      condition: Condition.targetIsYou(),
      delaySeconds: 13,
      run: (pull) => {
        pull.hasHellAwaits = false;
      }
    }),
    raws({
      id: "M9S Ultrasonic Spread",
      type: "StartsUsing",
      netRegex: { id: "B39C", source: "Vamp Fatale", capture: false },
      infoText: (pull, _hit, voices) => {
        return voices.text({
          avoid: pull.hasHellAwaits ? `${voices.avoid()} ` : "",
          mech: voices.rolePositions()
        });
      },
      outputStrings: {
        rolePositions: Voices.rolePositions,
        avoid: {
          en: "Avoid",
          de: "Vermeide",
          fr: "\xC9vitez : ",
          ja: "\u56DE\u907F",
          cn: "\u907F\u5F00",
          ko: "\uD53C\uD558\uAE30:"
        },
        text: {
          en: "${avoid}${mech}",
          de: "${avoid}${mech}",
          fr: "${avoid}${mech}",
          ja: "${mech}${avoid}",
          cn: "${avoid}${mech}",
          ko: "${avoid}${mech}"
        }
      }
    }),
    raws({
      id: "M9S Ultrasonic Amp",
      type: "StartsUsing",
      netRegex: { id: "B39D", source: "Vamp Fatale", capture: false },
      infoText: (pull, _hit, voices) => {
        return voices.text({
          avoid: pull.hasHellAwaits ? `${voices.avoid()} ` : "",
          mech: voices.stack()
        });
      },
      outputStrings: {
        stack: Voices.getTogether,
        avoid: {
          en: "Avoid",
          de: "Vermeide",
          fr: "\xC9vitez : ",
          ja: "\u56DE\u907F",
          cn: "\u907F\u5F00",
          ko: "\uD53C\uD558\uAE30:"
        },
        text: {
          en: "${avoid}${mech}",
          de: "${avoid}${mech}",
          fr: "${avoid}${mech}",
          ja: "${mech}${avoid}",
          cn: "${avoid}${mech}",
          ko: "${avoid}${mech}"
        }
      }
    }),
    raws({
      id: "M9S Undead Deathmatch",
      type: "StartsUsing",
      netRegex: { id: "B3A0", source: "Vamp Fatale", capture: false },
      response: Response.getTowers("alert")
    })
  ]
});
