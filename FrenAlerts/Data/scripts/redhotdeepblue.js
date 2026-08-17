const stageTable = {
  "B381": "snaking",
  "B5D4": "arenaSplit",
  "B5AE": "xtremeSnaking"
};
const headSignState = {
  "sharedBusterRed": "0103",
  "spreadFirePuddleRed": "0294",
  "partyStackFire": "029A",
  "blueTether": "027B",
  "redTether": "027C",
  "partnerStack": "0293",
  "closeTether": "017B",
  "farTether": "017A",
  "sickSwellTether": "0174"
};
const centers = {
  x: 100,
  y: 100
};
const snakingSlot = {
  "NW": "16",
  "N": "0F",
  "NE": "10",
  "W": "15",
  "C": "0E",
  "E": "11",
  "SW": "14",
  "S": "13",
  "SE": "12"
};
const snakingFlag = {
  "00020001": {
    elem: "water",
    mech: "protean"
  },
  "00200010": {
    elem: "water",
    mech: "stack"
  },
  "00800040": {
    elem: "water",
    mech: "buster"
  },
  "02000100": {
    elem: "fire",
    mech: "protean"
  },
  "08000400": {
    elem: "fire",
    mech: "stack"
  },
  "20001000": {
    elem: "fire",
    mech: "buster"
  }
};

defineDuty({
  id: "AacHeavyweightM2Savage",
  name: "M10S - Red Hot & Deep Blue",
  category: "Savage \u2013 Dawntrail",
  zoneId: 1323,
  boss: "Red Hot",
  center: centers,
  state: {
    phase: "one",
    actorPositions: {},
    dareCount: 0,
    waveDir: "unknown",
    snakingWater: null,
    snakingFire: null,
    snakingCount: 0
  },
  mechanics: [
    raws({
      id: "M10S Phase Tracker",
      type: "StartsUsing",
      netRegex: { id: Object.keys(stageTable), source: "Red Hot" },
      suppressSeconds: 1,
      run: (pull, hit) => {
        const stage = stageTable[hit.id];
        if (stage === void 0)
          throw new UnreachableCod();
        pull.phase = stage;
        if (stage === "snaking" || stage === "xtremeSnaking") {
          pull.snakingWater = null;
          pull.snakingFire = null;
          pull.snakingCount = 0;
        }
      }
    }),
    raws({
      id: "M10S ActorSetPos Tracker",
      type: "ActorSetPos",
      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      run: (pull, hit) => pull.actorPositions[hit.id] = {
        x: parseFloat(hit.x),
        y: parseFloat(hit.y),
        heading: parseFloat(hit.heading)
      }
    }),
    raws({
      id: "M10S AddedCombatant Tracker",
      type: "AddedCombatant",
      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      run: (pull, hit) => pull.actorPositions[hit.id] = {
        x: parseFloat(hit.x),
        y: parseFloat(hit.y),
        heading: parseFloat(hit.heading)
      }
    }),
    raws({
      id: "M10S Divers' Dare Collect",
      type: "StartsUsing",
      netRegex: { id: ["B5B8", "B5B9"], source: ["Red Hot", "Deep Blue"], capture: false },
      run: (pull) => pull.dareCount = pull.dareCount + 1
    }),
    raws({
      id: "M10S Divers' Dare",
      type: "StartsUsing",
      netRegex: { id: ["B5B8", "B5B9"], source: ["Red Hot", "Deep Blue"], capture: false },
      delaySeconds: 0.1,
      suppressSeconds: 1,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          aoe: Voices.aoe,
          bigAoe: Voices.bigAoe
        };
        if (pull.dareCount === 1)
          return { infoText: voice.aoe() };
        return { alertText: voice.bigAoe() };
      },
      run: (pull) => pull.dareCount = 0
    }),
    raws({
      id: "M10S Hot Impact Buster",
      type: "HeadMarker",
      netRegex: { id: headSignState["sharedBusterRed"], capture: true },
      response: (pull, hit, voice) => {
        voice.responseOutputStrings = {
          onYou: Voices.sharedTankbusterOnYou,
          shared: Voices.sharedTankbuster,
          avoid: Voices.avoidTankCleave
        };
        if (pull.role === "tank") {
          if (hit && hit.target === pull.me)
            return { alarmText: voice.onYou() };
          return { alertText: voice.shared() };
        }
        return { infoText: voice.avoid() };
      }
    }),
    raws({
      id: "M10S Flame Floater Order",
      type: "GainsEffect",
      netRegex: { effectId: ["BBC", "BBD", "BBE", "D7B"], capture: true },
      condition: Condition.targetIsYou(),
      infoText: (_pull, hit, voice) => {
        switch (hit.effectId) {
          case "BBC":
            return voice.bait({ order: "1" });
          case "BBD":
            return voice.bait({ order: "2" });
          case "BBE":
            return voice.bait({ order: "3" });
          case "D7B":
            return voice.bait({ order: "4" });
        }
      },
      outputStrings: {
        bait: {
          en: "${order}",
          de: "${order}",
          fr: "${order}",
          cn: "${order}",
          ko: "${order}"
        }
      }
    }),
    raws({
      id: "M10S Flame Floater and Hot Aerial Move",
      type: "GainsEffect",
      netRegex: { effectId: "B79", capture: true },
      condition: Condition.targetIsYou(),
      response: Response.moveAway()
    }),
    raws({
      id: "M10S Alley-oop Inferno Spread",
      type: "HeadMarker",
      netRegex: { id: headSignState["spreadFirePuddleRed"], capture: true },
      condition: Condition.targetIsYou(),
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          spread: Voices.spread,
          spreadFinal: {
            en: "Out + Spread => Stack Near Blue",
            de: "Raus + verteilen => Nahe Blau sammeln",
            fr: "Ext\xE9rieur + Dispersion => Package pr\xE8s de Blue",
            cn: "\u8FDC\u79BB + \u5206\u6563 => \u9760\u8FD1\u6DF1\u84DD\u96C6\u5408",
            ko: "\uBC16\uC73C\uB85C + \uC0B0\uAC1C => \uBE14\uB8E8 \uAC00\uAE4C\uC774 \uBAA8\uC774\uAE30"
          },
          spreadFinalBait: {
            en: "Out + Spread => Bait Blue Knockback Buster",
            de: "Raus + verteilen => K\xF6der blauen R\xFCcksto\xDF-Tankbuster",
            fr: "Ext\xE9rieur + Dispersion => D\xE9posez le tankbuster de Blue",
            cn: "\u8FDC\u79BB + \u5206\u6563 => \u5F15\u5BFC\u6DF1\u84DD\u5766\u514B\u51FB\u9000\u6B7B\u5211",
            ko: "\uBC16\uC73C\uB85C + \uC0B0\uAC1C => \uBE14\uB8E8 \uB109\uBC31 \uD0F1\uBC84 \uC720\uB3C4"
          }
        };
        if (pull.phase === "xtremeSnaking") {
          if (pull.role === "tank")
            return { infoText: voice.spreadFinalBait() };
          return { alertText: voice.spreadFinal() };
        }
        return { infoText: voice.spread() };
      }
    }),
    raws({
      id: "M10S Cutback Blaze",
      type: "StartsUsing",
      netRegex: { id: "B5C9", source: "Red Hot", capture: false },
      condition: (pull) => {
        return pull.snakingDebuff !== "water";
      },
      infoText: (_pull, _hit, voice) => voice.cleaveTowardsFire(),
      outputStrings: {
        cleaveTowardsFire: {
          en: "Bait cleave towards Fire",
          de: "K\xF6der Kegel-AoE Richtung Feuer",
          fr: "D\xE9posez le cleave vers le Feu",
          cn: "\u5411\u706B\u533A\u5F15\u5BFC\u6247\u5F62\u4F24\u5BB3",
          ko: "\uD654\uC5FC \uAD6C\uC5ED \uCABD\uC73C\uB85C \uBD80\uCC44\uAF34 \uC720\uB3C4"
        }
      }
    }),
    raws({
      id: "M10S Pyrotation Stack",
      type: "HeadMarker",
      netRegex: { id: headSignState["partyStackFire"], capture: true },
      condition: (pull) => {
        if (pull.role === "tank" && pull.phase === "xtremeSnaking")
          return false;
        return true;
      },
      alertText: (pull, hit, voice) => {
        const markThree = hit.target;
        if (markThree === pull.me) {
          const stack = "stackOnYou";
          return pull.phase === "xtremeSnaking" ? voice.stackFinal({ stack: voice[stack]() }) : voice[stack]();
        }
        if (markThree === void 0) {
          const stack = "stackOnMarker";
          return pull.phase === "xtremeSnaking" ? voice.stackFinal({ stack: voice[stack]() }) : voice[stack]();
        }
        if (pull.phase === "xtremeSnaking") {
          return voice.stackFinal({
            stack: voice.stackOnTarget({
              player: pull.party.member(markThree)
            })
          });
        }
        return voice.stackOnTarget({ player: pull.party.member(markThree) });
      },
      outputStrings: {
        stackOnYou: Voices.stackOnYou,
        stackOnTarget: Voices.stackOnPlayer,
        stackMarker: Voices.stackMarker,
        stackFinal: {
          en: "${stack} Near Blue",
          de: "${stack} Nahe Blau",
          fr: "${stack} pr\xE8s de Blue",
          cn: "${stack} \u9760\u8FD1\u6DF1\u84DD",
          ko: "${stack}: \uBE14\uB8E8 \uAC00\uAE4C\uC774"
        }
      }
    }),
    raws({
      id: "M10S Sickest Take-off Debuff",
      type: "GainsEffect",
      netRegex: {
        effectId: "808",
        count: ["3ED", "3EE", "3EF", "3F0"],
        capture: true
      },
      durationSeconds: 9,
      infoText: (pull, hit, voice) => {
        let bit;
        switch (hit.count) {
          case "3ED":
            bit = "healerGroups";
            break;
          case "3EE":
            bit = "spread";
            break;
          case "3EF":
            bit = pull.snakingDebuff === "fire" ? "waterStackFireDebuff" : "waterStack";
            break;
          case "3F0":
            bit = pull.snakingDebuff === "fire" ? "waterSpreadFireDebuff" : "waterSpread";
            break;
          default:
            return;
        }
        return voice[bit]();
      },
      outputStrings: {
        healerGroups: Voices.healerGroups,
        spread: Voices.spread,
        waterStack: {
          en: "Water Stack",
          de: "Wasser sammeln",
          fr: "Package Eau",
          cn: "\u6C34\u5206\u644A",
          ko: "\uBB3C \uC250\uC5B4"
        },
        waterStackFireDebuff: {
          en: "Water Stack",
          de: "Wasser sammeln",
          fr: "Package Eau",
          cn: "\u6C34\u5206\u644A",
          ko: "\uBB3C \uC250\uC5B4"
        },
        waterSpread: Voices.spread,
        waterSpreadFireDebuff: {
          en: "Avoid Water Players",
          de: "Vermeide Wasser-Spieler",
          fr: "\xC9vitez les joueurs Eau",
          cn: "\u8FDC\u79BB\u6C34\u7EC4\u73A9\u5BB6",
          ko: "\uBB3C \uD50C\uB808\uC774\uC5B4 \uD53C\uD558\uAE30"
        }
      }
    }),
    raws({
      id: "M10S Sick Swell Wave Collector",
      type: "Tether",
      netRegex: { source: "Deep Blue", id: headSignState["sickSwellTether"], capture: true },
      delaySeconds: 0.1,
      run: (pull, hit) => {
        const actor = pull.actorPositions[hit.targetId];
        if (actor === void 0)
          return;
        pull.waveDir = Facings.xyToCardinalDirOutput(actor.x, actor.y, centers.x, centers.y);
      }
    }),
    raws({
      id: "M10S Sick Swell",
      type: "StartsUsingExtra",
      netRegex: { id: "B5CE", capture: true },
      delaySeconds: 1,
      alertText: (pull, hit, voice) => {
        const chantX = parseFloat(hit.x);
        const chantY = parseFloat(hit.y);
        const kbFacing = pull.waveDir;
        if (kbFacing === "dirE" || kbFacing === "dirW") {
          if (chantY < 95)
            return voice.text({ dir1: voice[kbFacing](), dir2: voice["dirN"]() });
          else if (chantY > 105)
            return voice.text({ dir1: voice[kbFacing](), dir2: voice["dirS"]() });
          return voice.text({ dir1: voice[kbFacing](), dir2: voice.middle() });
        }
        if (chantX < 95)
          return voice.text({ dir1: voice[kbFacing](), dir2: voice["dirW"]() });
        else if (chantX > 105)
          return voice.text({ dir1: voice[kbFacing](), dir2: voice["dirE"]() });
        return voice.text({ dir1: voice[kbFacing](), dir2: voice.middle() });
      },
      outputStrings: {
        middle: Voices.middle,
        text: {
          en: "KB from ${dir1} + away from ${dir2}",
          de: "R\xFCcksto\xDF von ${dir1} + weg von ${dir2}",
          fr: "Pouss\xE9e depuis ${dir1} + loin de ${dir2}",
          cn: "\u4ECE${dir1}\u51FB\u9000 + \u8FDC\u79BB${dir2}",
          ko: "${dir1}\uC5D0\uC11C \uB109\uBC31 + ${dir2}\uCABD \uD53C\uD558\uAE30"
        },
        ...Facings.outputStringsCardinalDir
      }
    }),
    raws({
      id: "M10S Sick Swell Wave Collector (Snaking)",
      type: "Tether",
      netRegex: { source: "Deep Blue", id: headSignState["sickSwellTether"], capture: true },
      condition: (pull) => pull.phase === "snaking",
      delaySeconds: 0.1,
      durationSeconds: 10.2,
      suppressSeconds: 9999,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.targetId];
        if (actor === void 0)
          return;
        const waveFacing = Facings.xyTo4DirNum(actor.x, actor.y, centers.x, centers.y);
        const waveFacingVoice = Facings.outputCardinalDir[waveFacing];
        const coneFacing = (Facings.xyTo4DirNum(actor.x, actor.y, centers.x, centers.y) + 2) % 4;
        const coneFacingVoice = Facings.outputCardinalDir[coneFacing];
        return voice.text({
          waveDir: voice[waveFacingVoice ?? "unknown"](),
          coneDir: voice[coneFacingVoice ?? "unknown"]()
        });
      },
      outputStrings: {
        text: {
          en: "Wave ${waveDir}/Cone ${coneDir}",
          de: "Welle ${waveDir}/Kegel ${coneDir}",
          fr: "Vague ${waveDir} / C\xF4ne ${coneDir}",
          cn: "${waveDir} \u51FB\u9000/${coneDir} \u4E24\u4FA7",
          ko: "\uD30C\uB3C4 ${waveDir}/\uBD80\uCC44\uAF34 ${coneDir}"
        },
        ...Facings.outputStringsCardinalDir
      }
    }),
    raws({
      id: "M10S Reverse Alley-oop/Alley-oop Double-dip",
      type: "StartsUsing",
      netRegex: { id: ["B5E0", "B5DD"], source: "Deep Blue", capture: true },
      condition: (pull) => {
        return pull.snakingDebuff !== "fire";
      },
      durationSeconds: (pull) => pull.phase === "arenaSplit" ? 8 : 3,
      infoText: (pull, hit, voice) => {
        if (pull.phase === "snaking")
          return voice.watersnaking({
            protean: voice.protean(),
            action: voice.move()
          });
        if (pull.phase === "arenaSplit") {
          return hit.id === "B5E0" ? voice.arenaSplitReverse() : voice.arenaSplitDoubleDip();
        }
        const action = hit.id === "B5E0" ? voice.stay() : voice.move();
        return voice.text({ protean: voice.protean(), action });
      },
      outputStrings: {
        protean: Voices.protean,
        move: Voices.moveAway,
        stay: {
          en: "Stay",
          de: "Bleib stehen",
          fr: "Restez",
          cn: "\u505C",
          ko: "\uB300\uAE30",
          tc: "\u505C"
        },
        text: {
          en: "${protean} => ${action}",
          de: "${protean} => ${action}",
          fr: "${protean} => ${action}",
          cn: "${protean} => ${action}",
          ko: "${protean} => ${action}"
        },
        watersnaking: {
          en: "${protean} => ${action}",
          de: "${protean} => ${action}",
          fr: "${protean} => ${action}",
          cn: "${protean} => ${action}",
          ko: "${protean} => ${action}"
        },
        arenaSplitReverse: {
          en: "Reverse Alley-oop",
          de: "Umgekehrter Alley-Oop",
          fr: "Alley-oop invers\xE9",
          cn: "\u8981\u505C",
          ko: "\uB300\uAE30"
        },
        arenaSplitDoubleDip: {
          en: "Double-Dip Protean",
          de: "Doppel-Alley-Oop",
          fr: "Double Alley-oop",
          cn: "\u8981\u52A8",
          ko: "\uC774\uB3D9"
        }
      }
    }),
    raws({
      id: "M10S Reverse Alley-oop/Alley-oop Double-dip 2nd Hit",
      type: "Ability",
      netRegex: { id: ["B5E0", "B5DD"], source: "Deep Blue", capture: true },
      condition: (pull) => {
        return pull.snakingDebuff !== "fire" && pull.phase !== "arenaSplit";
      },
      infoText: (pull, hit, voice) => {
        if (pull.phase === "snaking")
          return voice.move();
        return hit.id === "B5E0" ? voice.stay() : voice.move();
      },
      outputStrings: {
        move: Voices.moveAway,
        stay: {
          en: "Stay",
          de: "Bleib stehen",
          fr: "Restez",
          cn: "\u505C",
          ko: "\uB300\uAE30",
          tc: "\u505C"
        }
      }
    }),
    raws({
      id: "M10S Xtreme Spectacular",
      type: "StartsUsing",
      netRegex: { id: "B5D9", source: "Red Hot", capture: true },
      delaySeconds: (_pull, hit) => parseFloat(hit.castTime),
      alertText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Go N/S + Big AoE",
          de: "Geh N/S + Gro\xDFe AoE",
          fr: "Allez N/S + Grosse AoE",
          cn: "\u53BB\u4E0A/\u4E0B + \u9AD8\u4F24\u5BB3 AOE",
          ko: "\uB0A8/\uBD81\uCABD\uC73C\uB85C + \uAC15\uD55C \uC804\uCCB4 \uACF5\uACA9"
        }
      }
    }),
    raws({
      id: "M10S Snaking Flags Collector",
      type: "MapEffect",
      netRegex: {
        location: Object.values(snakingSlot),
        flags: Object.keys(snakingFlag),
        capture: true
      },
      preRun: (pull, hit) => {
        const snakings = snakingFlag[hit.flags];
        if (snakings === void 0)
          return;
        if (snakings.elem === "water")
          pull.snakingWater = snakings.mech;
        else
          pull.snakingFire = snakings.mech;
      },
      durationSeconds: 6,
      infoText: (pull, _hit, voice) => {
        const waters = pull.snakingWater;
        const fir = pull.snakingFire;
        if (waters === null || fir === null)
          return;
        if (pull.phase !== "xtremeSnaking") {
          return voice.pair({
            water: voice.water(),
            waterMech: voice[waters](),
            fire: voice.fire(),
            fireMech: voice[fir]()
          });
        }
        let trade;
        if (fir === "buster")
          trade = "tank";
        else if (pull.snakingCount === 0)
          trade = "healer";
        else if (pull.snakingCount === 1)
          trade = "melee";
        else
          trade = "ranged";
        return voice.pairSwap({
          water: voice.water(),
          waterMech: voice[waters](),
          fire: voice.fire(),
          fireMech: voice[fir](),
          swap: voice.swapText({ role: voice[trade]() })
        });
      },
      run: (pull) => {
        if (pull.snakingWater === null || pull.snakingFire === null)
          return;
        if (pull.phase === "xtremeSnaking" && pull.snakingFire !== "buster")
          pull.snakingCount++;
        pull.snakingWater = null;
        pull.snakingFire = null;
      },
      outputStrings: {
        pair: {
          en: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red>",
          de: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red>",
          fr: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red>",
          cn: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red>",
          ko: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red>"
        },
        pairSwap: {
          en: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red> (${swap})",
          de: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red> (${swap})",
          fr: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red> (${swap})",
          cn: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red> (${swap})",
          ko: "<blue>${water}: ${waterMech}</blue> / <red>${fire}: ${fireMech}</red> (${swap})"
        },
        fire: {
          en: "Fire",
          de: "Feuer",
          fr: "Feu",
          cn: "\u706B",
          ko: "\uBD88"
        },
        water: {
          en: "Water",
          de: "Wasser",
          fr: "Eau",
          cn: "\u6C34",
          ko: "\uBB3C"
        },
        stack: Voices.getTogether,
        protean: Voices.protean,
        buster: {
          en: "Buster",
          de: "Tankbuster",
          fr: "Buster",
          cn: "\u6B7B\u5211",
          ko: "\uD0F1\uBC84"
        },
        swapText: {
          en: "${role} Swap",
          de: "${role} wechsel",
          fr: "\xC9change ${role}",
          cn: "${role} \u4EA4\u6362",
          ko: "${role} \uAD50\uB300"
        },
        tank: Voices.tank,
        healer: Voices.healer,
        melee: {
          en: "Melee",
          de: "Nahk\xE4mpfer",
          fr: "M\xEAl\xE9e",
          cn: "\u8FD1\u6218",
          ko: "\uADFC\uB51C"
        },
        ranged: {
          en: "Ranged",
          de: "Fernk\xE4mpfer",
          fr: "Distant",
          cn: "\u8FDC\u7A0B",
          ko: "\uC6D0\uB51C"
        }
      }
    }),
    raws({
      id: "M10S Deep Impact Buster",
      type: "StartsUsing",
      netRegex: { id: "B5B7", source: "Deep Blue", capture: false },
      condition: (pull) => {
        if (pull.role !== "tank" && pull.phase === "xtremeSnaking")
          return false;
        return pull.snakingDebuff !== "fire";
      },
      infoText: (pull, _hit, voice) => {
        if (pull.role === "tank")
          return voice.baitBlueBuster();
        return voice.beNearBlue();
      },
      outputStrings: {
        beNearBlue: {
          en: "Be Near Blue",
          de: "Sei nahe Blau",
          fr: "Pr\xE8s de Blue",
          cn: "\u9760\u8FD1\u6DF1\u84DD",
          ko: "\uBE14\uB8E8 \uAC00\uAE4C\uC774 \uC788\uAE30"
        },
        baitBlueBuster: {
          en: "Bait Blue Knockback Buster",
          de: "K\xF6dere blauen R\xFCcksto\xDF-Tankbuster",
          fr: "D\xE9posez le tankbuster de Blue (pouss\xE9e)",
          cn: "\u5F15\u5BFC\u6DF1\u84DD\u5766\u514B\u51FB\u9000\u6B7B\u5211",
          ko: "\uBE14\uB8E8 \uB109\uBC31 \uD0F1\uBC84 \uC720\uB3C4"
        }
      }
    }),
    whenChant("B381").bigAoe(),
    raws({
      id: "M10S Snaking Debuff Collect",
      type: "GainsEffect",
      netRegex: { effectId: ["136E", "136F"], capture: true },
      condition: Condition.targetIsYou(),
      run: (pull, hit) => {
        pull.snakingDebuff = hit.effectId === "136E" ? "fire" : "water";
      }
    }),
    raws({
      id: "M10S Snaking Debuff Cleanup",
      type: "LosesEffect",
      netRegex: { effectId: ["136E", "136F"], capture: true },
      condition: Condition.targetIsYou(),
      run: (pull) => pull.snakingDebuff = void 0
    }),
    raws({
      id: "M10S Snaking Debuff Target",
      type: "GainsEffect",
      netRegex: { effectId: ["136E", "136F"], capture: true },
      condition: Condition.targetIsYou(),
      infoText: (_pull, hit, voice) => {
        if (hit.effectId === "136E")
          return voice.firesnaking();
        return voice.watersnaking();
      },
      outputStrings: {
        firesnaking: {
          en: "Red's Target",
          de: "Rotes Ziel",
          fr: "Cibl\xE9 par Red",
          cn: "\u706B\u7EC4",
          ko: "\uB808\uB4DC"
        },
        watersnaking: {
          en: "Blue's Target",
          de: "Blaues Ziel",
          fr: "Cibl\xE9 par Blue",
          cn: "\u6C34\u7EC4",
          ko: "\uBE14\uB8E8"
        }
      }
    }),
    raws({
      id: "M10S Deep Varial",
      type: "MapEffect",
      netRegex: {
        location: ["02", "04"],
        flags: ["00800040", "08000400"],
        capture: true
      },
      infoText: (_pull, hit, voice) => {
        const facingTwo = hit.location === "02" ? "north" : "south";
        const bit = hit.flags === "00800040" ? "stack" : "spread";
        return voice.text({
          dir: voice[facingTwo](),
          mech: voice[bit]()
        });
      },
      outputStrings: {
        north: Voices.north,
        south: Voices.south,
        stack: {
          en: "Water Stack",
          de: "Wasser sammeln",
          fr: "Package Eau",
          cn: "\u6C34\u5206\u644A",
          ko: "\uBB3C \uC250\uC5B4"
        },
        spread: {
          en: "Water Spread",
          de: "Wasser verteilen",
          fr: "Dispersion Eau",
          cn: "\u6C34\u5206\u6563",
          ko: "\uBB3C \uC0B0\uAC1C"
        },
        text: {
          en: "${dir} + ${mech} + Fire Spread",
          de: "${dir} + ${mech} + Feuer verteilen",
          fr: "${dir} + ${mech} + Dispersion Feu",
          cn: "${dir} + ${mech} + \u706B\u5206\u6563",
          ko: "${dir} + ${mech} + \uBD88 \uC0B0\uAC1C"
        }
      }
    }),
    raws({
      id: "M10S Hot Aerial",
      type: "StartsUsing",
      netRegex: { id: "B5C4", source: "Red Hot", capture: false },
      condition: (pull) => {
        return pull.snakingDebuff === "fire";
      },
      infoText: (_pull, _hit, voice) => voice.baitHotAerial(),
      outputStrings: {
        baitHotAerial: {
          en: "Bait Hot Aerial",
          de: "K\xF6der Flammensprung",
          fr: "D\xE9posez Flamme a\xE9rienne",
          cn: "\u5F15\u5BFC\u56DB\u8FDE\u8DF3",
          ko: "\uBD88\uAF43 \uACF5\uC911\uD68C\uC804 \uC720\uB3C4"
        }
      }
    }),
    raws({
      id: "M10S Deep Aerial Tower",
      type: "StartsUsing",
      netRegex: { id: "B5E3", source: "Deep Blue", capture: false },
      infoText: (_pull, _hit, voice) => voice.getTower(),
      outputStrings: {
        getTower: {
          en: "Get Tower",
          de: "Turm nehmen",
          fr: "Prenez la tour",
          ja: "\u5854\u3092\u8E0F\u3080",
          cn: "\u8E29\u5854",
          ko: "\uD0D1 \uBC1F\uAE30",
          tc: "\u8E29\u5854"
        }
      }
    }),
    raws({
      id: "M10S Xtreme Wave Tethers",
      type: "HeadMarker",
      netRegex: {
        id: [headSignState["redTether"], headSignState["blueTether"]],
        capture: true
      },
      condition: Condition.targetIsYou(),
      infoText: (_pull, hit, voice) => {
        if (hit.id === headSignState["redTether"])
          return voice.redTether();
        return voice.blueTether();
      },
      outputStrings: {
        redTether: {
          en: "Red Tether on YOU",
          de: "Rote Verbindung auf DIR",
          fr: "Lien Rouge sur VOUS",
          cn: "\u706B\u7EBF\u70B9\u540D",
          ko: "\uB808\uB4DC \uC120 \uB300\uC0C1\uC790"
        },
        blueTether: {
          en: "Blue Tether on YOU",
          de: "Blaue Verbindung auf DIR",
          fr: "Lien Bleu sur VOUS",
          cn: "\u6C34\u7EBF\u70B9\u540D",
          ko: "\uBE14\uB8E8 \uC120 \uB300\uC0C1\uC790"
        }
      }
    }),
    raws({
      id: "M10S Flame Floater Split",
      type: "StartsUsing",
      netRegex: { id: "B5D4", source: "Red Hot", capture: false },
      alertText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "E/W Groups, Out of Middle",
          de: "O/W Gru\xDF\xDFen, Raus aus der Mitte",
          fr: "Groupes E/O, Sortez du milieu",
          cn: "\u5DE6\u53F3\u5206\u7EC4\uFF0C\u8FDC\u79BB\u4E2D\u95F4",
          ko: "\uB3D9/\uC11C \uADF8\uB8F9, \uC911\uC559 \uD53C\uD558\uAE30"
        }
      }
    }),
    whenChant("B5AE").bigAoe(),
    raws({
      id: "M10S Xtreme Firesnaking/WaterSnaking Debuffs",
      type: "GainsEffect",
      netRegex: { effectId: ["12DB", "12DC"], capture: true },
      condition: Condition.targetIsYou(),
      infoText: (_pull, hit, voice) => {
        if (hit.effectId === "12DB")
          return voice.xtremeFiresnaking();
        return voice.xtremeWatersnaking();
      },
      outputStrings: {
        xtremeFiresnaking: {
          en: "Red Debuff (Fire)",
          de: "Roter Debuff (Feuer)",
          fr: "Debuff Rouge (Feu)",
          cn: "\u706B Debuff",
          ko: "\uB808\uB4DC \uB514\uBC84\uD504 (\uBD88)"
        },
        xtremeWatersnaking: {
          en: "Blue Debuff (Water)",
          de: "Blauer Debuff (Wasser)",
          fr: "Debuff Bleu (Eau)",
          cn: "\u6C34 Debuff",
          ko: "\uBE14\uB8E8 \uB514\uBC84\uD504 (\uBB3C)"
        }
      }
    })
  ]
});
