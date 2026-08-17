

const centers = {
  x: 100,
  y: 100
};
const stageTable = {
  "B43F": "arenaSplit",
  "B448": "avalanche",
  "B452": "ecliptic"
};
const headSignState = {
  "cometSpread": "008B",
  "partnerStack": "00A1",
  "fiveHitStack": "0131",
  "meteor": "00F4",
  "fireBreath": "00F4",
  "lineStack": "020D",
  "atomicImpact": "001E",
  "meteorTether": "0164",
  "closeTether": "0039",
  "farTether": "00F9"
};
const ultimateTrophyWeaponsTable = [
  void 0,
  void 0,
  {
    delay: 0,
    duration: 8.7
  },
  {
    delay: 4.7,
    duration: 5.1
  },
  {
    delay: 5.8,
    duration: 5.1
  },
  {
    delay: 6.9,
    duration: 5.1
  },
  {
    delay: 8,
    duration: 5.1
  },
  {
    delay: 9.1,
    duration: 5.1
  }
];

defineDuty({
  id: "AacHeavyweightM3Savage",
  name: "M11S - The Tyrant & Comet",
  category: "Savage \u2013 Dawntrail",
  zoneId: 1325,
  boss: "The Tyrant",
  center: { x: 100, y: 100 },
  state: {
    phase: "one",
    actorPositions: {},
    weapons: [],
    weaponMechCount: 0,
    ultMechs: [],
    ultResolved: 0,
    comboOrder: [],
    comboResolved: 0,
    domDirectionCount: {
      horizCount: 0,
      vertCount: 0,
      outerSafe: ["dirN", "dirE", "dirS", "dirW"]
    },
    assaultEvolvedCount: 0,
    maelstromCount: 0,
    hasMeteor: false,
    arenaSplitTethers: [],
    arenaSplitCalledTether: false,
    arenaSplitCalledBait: false,
    towerKnockbackDir: void 0,
    fireballCount: 0,
    hasAtomic: false,
    hadEclipticTether: false,
    heartbreakerCount: 0,
    triggerSetConfig: {
      majesticMeteowrathTetherDir: "cw",
      twoWayFireballBaitDir: "ew"
    }
  },
  mechanics: [
    raws({
      id: "M11S Phase Tracker",
      type: "StartsUsing",
      netRegex: { id: Object.keys(stageTable), source: "The Tyrant" },
      suppressSeconds: 1,
      run: (pull, hit) => {
        const stage = stageTable[hit.id];
        if (stage === void 0)
          throw new UnreachableCod();
        pull.phase = stage;
      }
    }),
    raws({
      id: "M11S ActorSetPos Tracker",
      type: "ActorSetPos",
      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      run: (pull, hit) => pull.actorPositions[hit.id] = {
        x: parseFloat(hit.x),
        y: parseFloat(hit.y),
        heading: parseFloat(hit.heading)
      }
    }),
    whenChant("B406").bigAoe(),
    raws({
      id: "M11S Raw Steel Trophy Axe",
      type: "StartsUsing",
      netRegex: { id: "B422", capture: false },
      infoText: (_pull, _hit, voice) => {
        return voice.text({
          party: voice.partySpread(),
          tank: voice.sharedTankStack()
        });
      },
      outputStrings: {
        partySpread: {
          en: "Party Spread",
          de: "Party verteilen",
          cn: "\u4EBA\u7FA4\u5206\u6563",
          ko: "\uBCF8\uB300 \uC0B0\uAC1C"
        },
        sharedTankStack: {
          en: "Tanks Stack",
          de: "Tanks Sammeln",
          fr: "Package Tanks",
          ja: "\u30BF\u30F3\u30AF\u982D\u5272\u308A",
          cn: "\u5766\u514B\u5206\u644A",
          ko: "\uD0F1\uCEE4 \uC250\uC5B4",
          tc: "\u5766\u514B\u5206\u6524"
        },
        text: {
          en: "${party}/${tank}",
          de: "${party}/${tank}",
          cn: "${party}/${tank}",
          ko: "${party}/${tank}"
        }
      }
    }),
    raws({
      id: "M11S Raw Steel Trophy Scythe",
      type: "StartsUsing",
      netRegex: { id: "B423", capture: false },
      infoText: (_pull, _hit, voice) => {
        return voice.text({
          party: voice.partyStack(),
          tank: voice.tankCleaves()
        });
      },
      outputStrings: {
        partyStack: {
          en: "Party Stack",
          de: "In der Gruppe sammeln",
          fr: "Package en groupe",
          ja: "\u3042\u305F\u307E\u308F\u308A",
          cn: "\u4EBA\u7FA4\u5206\u644A",
          ko: "\uBCF8\uB300 \uC250\uC5B4",
          tc: "\u5206\u6524"
        },
        tankCleaves: {
          en: "Tank Cleaves",
          de: "Tank Cleaves",
          fr: "Tank Cleaves",
          ja: "\u30BF\u30F3\u30AF\u524D\u65B9\u653B\u6483",
          cn: "\u5766\u514B\u6247\u5F62",
          ko: "\uAD11\uC5ED \uD0F1\uBC84",
          tc: "\u5766\u514B\u9806\u5288"
        },
        text: {
          en: "${party}/${tank}",
          de: "${party}/${tank}",
          cn: "${party}/${tank}",
          ko: "${party}/${tank}"
        }
      }
    }),
    raws({
      id: "M11S Ultimate Trophy Weapons",
      type: "ActorControlExtra",
      netRegex: { category: "0197", param1: ["11D1", "11D2", "11D3"], capture: true },
      condition: (pull) => pull.weaponMechCount > 1,
      durationSeconds: 6,
      infoText: (pull, hit, voice) => {
        const mechanicsTwo = hit.param1 === "11D1" ? "healerGroups" : hit.param1 === "11D2" ? "stack" : "protean";
        pull.ultMechs.push(mechanicsTwo);
        if (pull.ultMechs.length === 1)
          return voice[mechanicsTwo]();
      },
      outputStrings: {
        healerGroups: Voices.healerGroups,
        stack: Voices.stackMiddle,
        protean: Voices.protean
      }
    }),
    raws({
      id: "M11S Ultimate Trophy Weapons Resolve",
      type: "ActorControlExtra",
      netRegex: { category: "0197", param1: ["11DD", "11DE", "11DF"], capture: false },
      condition: (pull) => pull.ultMechs.length > 0,
      durationSeconds: 6,
      infoText: (pull, _hit, voice) => {
        pull.ultResolved = pull.ultResolved + 1;
        const spot = pull.ultResolved;
        if (spot >= 6)
          return voice.bait();
        const mechanicsTwo = pull.ultMechs[spot];
        if (mechanicsTwo === void 0)
          return;
        if (spot >= 2 && mechanicsTwo !== "stack")
          return voice.mechanicThenMove({ mech: voice[mechanicsTwo](), move: voice.move() });
        return voice[mechanicsTwo]();
      },
      outputStrings: {
        healerGroups: Voices.healerGroups,
        stack: Voices.stackMiddle,
        protean: Voices.protean,
        move: Voices.moveAway,
        bait: {
          en: "Bait Gust",
          de: "B\xF6e k\xF6dern",
          cn: "\u8BF1\u5BFC\u5F3A\u98CE",
          ko: "\uAC15\uD48D \uC720\uB3C4"
        },
        mechanicThenMove: {
          en: "${mech} => ${move}",
          de: "${mech} => ${move}",
          cn: "${mech} => ${move}",
          ko: "${mech} => ${move}"
        },
        mechanicThenBait: {
          en: "${mech} => ${bait}",
          de: "${mech} => ${bait}",
          cn: "${mech} => ${bait}",
          ko: "${mech} => ${bait}"
        }
      }
    }),
    raws({
      id: "M11S Trophy Weapons 2 Early Calls",
      type: "ActorControlExtra",
      netRegex: { category: "0197", param1: ["11D1", "11D2", "11D3"], capture: true },
      condition: (pull, hit) => {
        return false;
        if (pull.weaponMechCount !== 1)
          return false;
        const actor = pull.actorPositions[hit.id];
        if (actor === void 0)
          return false;
        const bodyFacing = Math.atan2(actor.x - centers.x, actor.y - centers.y);
        if (Math.abs(bodyFacing - actor.heading) % Math.PI < 0.1)
          return true;
        return false;
      },
      suppressSeconds: 9999,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.id];
        if (actor === void 0)
          return;
        const mechanicsTwo = hit.param1 === "11D1" ? "healerGroups" : hit.param1 === "11D2" ? "stack" : "protean";
        const facingTwo = Facings.xyTo8DirOutput(actor.x, actor.y, centers.x, centers.y);
        return voice.text({
          dir: voice[facingTwo](),
          weapon: voice[mechanicsTwo]()
        });
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        healerGroups: Voices.healerGroups,
        stack: Voices.stackMiddle,
        protean: Voices.protean,
        text: {
          en: "${dir}: ${weapon} (1st later)",
          de: "${dir}: ${weapon} (erste sp\xE4ter)",
          cn: "${dir}: ${weapon} (\u7A0D\u540E\u7B2C\u4E00\u6CE2)",
          ko: "${dir}: ${weapon} (\uACE7 1\uBC88\uC9F8)"
        }
      }
    }),
    raws({
      id: "M11S Trophy Weapons",
      type: "ActorControlExtra",
      netRegex: { category: "0197", param1: ["11D1", "11D2", "11D3"], capture: true },
      condition: (pull) => pull.weaponMechCount < 2,
      delaySeconds: 0.1,
      durationSeconds: (pull) => pull.weaponMechCount === 0 ? 20.9 : 32,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.id];
        if (actor === void 0)
          return;
        pull.weapons.push({
          id: hit.id,
          type: hit.param1 === "11D1" ? "healerGroups" : hit.param1 === "11D2" ? "stack" : "protean",
          dir: Math.atan2(actor.x - centers.x, actor.y - centers.y),
          actor
        });
        if (pull.weapons.length > 2) {
          pull.weaponMechCount++;
          pull.comboResolved = 0;
          pull.comboOrder = [];
          let candidate = pull.weapons;
          pull.weapons = [];
          const weapon1s = candidate.find(
            (c) => Math.abs(c.dir - c.actor.heading) % Math.PI < 0.1
          );
          if (weapon1s === void 0)
            return;
          candidate = candidate.filter((c) => c !== weapon1s);
          candidate.forEach((c) => {
            c.dir = Math.atan2(c.actor.x - weapon1s.actor.x, c.actor.y - weapon1s.actor.y);
          });
          const weapon2s = candidate.find(
            (c) => Math.abs(c.dir - c.actor.heading) % Math.PI < 0.1
          );
          const weapon3s = candidate.find((c) => c !== weapon2s);
          if (weapon2s === void 0 || weapon3s === void 0)
            return;
          pull.comboOrder = [weapon1s.type, weapon2s.type, weapon3s.type];
          return voice.text({
            weapon1: voice[weapon1s.type](),
            weapon2: voice[weapon2s.type](),
            weapon3: voice[weapon3s.type]()
          });
        }
      },
      outputStrings: {
        text: {
          en: "${weapon1} => ${weapon2} => ${weapon3}",
          de: "${weapon1} => ${weapon2} => ${weapon3}",
          cn: "${weapon1} => ${weapon2} => ${weapon3}",
          ko: "${weapon1} => ${weapon2} => ${weapon3}"
        },
        healerGroups: Voices.healerGroups,
        stack: Voices.stackMiddle,
        protean: Voices.protean
      }
    }),
    raws({
      id: "M11S Trophy Weapons Resolve",
      type: "ActorControlExtra",
      netRegex: { category: "0197", param1: ["11DD", "11DE", "11DF"], capture: true },
      condition: (pull) => pull.ultMechs.length === 0,
      durationSeconds: 6,
      infoText: (pull, hit, voice) => {
        pull.comboResolved = pull.comboResolved + 1;
        const spot = pull.comboResolved;
        if (pull.comboOrder.length === 3) {
          const bit = pull.comboOrder[spot];
          if (spot >= 3)
            pull.comboOrder = [];
          if (bit === void 0)
            return;
          return voice[bit]();
        }
        const mechanicsTwo = hit.param1 === "11DD" ? "healerGroups" : hit.param1 === "11DE" ? "stack" : "protean";
        return voice[mechanicsTwo]();
      },
      outputStrings: {
        healerGroups: Voices.healerGroups,
        stack: Voices.stackMiddle,
        protean: Voices.protean
      }
    }),
    whenChant("B412").info("Bait 3x puddles"),
    raws({
      id: "M11S Comet Spread Collect",
      type: "HeadMarker",
      netRegex: { id: headSignState["cometSpread"], capture: false },
      suppressSeconds: 1,
      run: (pull) => {
        if (pull.voidStardust === void 0)
          pull.voidStardust = "spread";
      }
    }),
    whenSign(headSignState["cometSpread"]).onYou().spread(),
    raws({
      id: "M11S Crushing Comet Collect",
      type: "StartsUsing",
      netRegex: { id: "B415", source: "The Tyrant", capture: false },
      run: (pull) => {
        if (pull.voidStardust === void 0)
          pull.voidStardust = "stack";
      }
    }),
    raws({
      id: "M11S Crushing Comet",
      type: "StartsUsing",
      netRegex: { id: "B415", source: "The Tyrant", capture: true },
      response: Response.stackMarkerOn()
    }),
    raws({
      id: "M11S Void Stardust End",
      type: "StartsUsing",
      netRegex: { id: ["B418", "B419", "B41A"], source: "The Tyrant", capture: true },
      condition: (pull) => {
        if (pull.voidStardust === void 0)
          return false;
        pull.assaultEvolvedCount++;
        if (pull.assaultEvolvedCount === 3)
          return true;
        return false;
      },
      delaySeconds: (_pull, hit) => parseFloat(hit.castTime),
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        if (pull.voidStardust === "spread")
          return voice.baitPuddlesThenStack();
        if (pull.voidStardust === "stack")
          return voice.baitPuddlesThenSpread();
      },
      outputStrings: {
        baitPuddlesThenStack: {
          en: "Bait 3x Puddles => Stack",
          de: "K\xF6dere Fl\xE4che x3 => Sammeln",
          cn: "\u8BF1\u5BFC3\u6B21\u5708\u5708 => \u5206\u644A",
          ko: "\uC7A5\uD310 \uC720\uB3C4 3x => \uC250\uC5B4"
        },
        baitPuddlesThenSpread: {
          en: "Bait 3x Puddles => Spread",
          de: "K\xF6dere Fl\xE4che x3 => Verteilen",
          cn: "\u8BF1\u5BFC3\u6B21\u5708\u5708 => \u5206\u6563",
          ko: "\uC7A5\uD310 \uC720\uB3C4 3x => \uC0B0\uAC1C"
        }
      }
    }),
    whenChant("B7BB").after(3.7).hold(5).info("AoE x6 => Big AoE"),
    raws({
      id: "M11S Dance Of Domination Trophy Safe Spots",
      type: "StartsUsingExtra",
      netRegex: { id: "B7BC", capture: true },
      preRun: (pull, hit) => {
        const headingFacingDigit = Facings.hdgTo8DirNum(parseFloat(hit.heading));
        if (headingFacingDigit % 2 !== 0)
          return;
        const isVerts = headingFacingDigit % 4 === 0;
        let dangerFacing = void 0;
        if (isVerts) {
          pull.domDirectionCount.vertCount += 1;
          if (parseFloat(hit.x) < centers.x - 5)
            dangerFacing = "dirW";
          else if (parseFloat(hit.x) > centers.x + 5)
            dangerFacing = "dirE";
        } else {
          pull.domDirectionCount.horizCount += 1;
          if (parseFloat(hit.y) < centers.y - 5)
            dangerFacing = "dirN";
          else if (parseFloat(hit.y) > centers.y + 5)
            dangerFacing = "dirS";
        }
        if (dangerFacing !== void 0)
          pull.domDirectionCount.outerSafe = pull.domDirectionCount.outerSafe.filter(
            (facingTwo) => facingTwo !== dangerFacing
          );
      },
      infoText: (pull, _hit, voice) => {
        if (pull.domDirectionCount.outerSafe.length !== 1)
          return;
        const outerClearFacing = pull.domDirectionCount.outerSafe[0];
        if (outerClearFacing === void 0)
          return;
        if (pull.domDirectionCount.vertCount === 1)
          return voice.northSouth({ dir: voice[outerClearFacing]() });
        else if (pull.domDirectionCount.horizCount === 1)
          return voice.eastWest({ dir: voice[outerClearFacing]() });
      },
      run: (pull) => {
        if (pull.domDirectionCount.outerSafe.length === 1)
          pull.domDirectionCount.outerSafe = [];
      },
      outputStrings: {
        northSouth: {
          en: "N/S Mid / ${dir} Outer + Partner Stacks",
          de: "N/S Mitte / ${dir} Au\xDFen + mit Partner sammeln",
          cn: "\u4E0A/\u4E0B\u4E2D\u95F4 / ${dir} \u5916\u4FA7 + \u961F\u53CB\u5206\u644A",
          ko: "\uBD81/\uB0A8 \uC911\uAC04 / ${dir} \uBC14\uAE65 + \uD30C\uD2B8\uB108 \uC250\uC5B4"
        },
        eastWest: {
          en: "E/W Mid / ${dir} Outer + Partner Stacks",
          de: "O/W Mitte / ${dir} Au\xDFen + mit Partner sammeln",
          cn: "\u5DE6/\u53F3\u4E2D\u95F4 / ${dir} \u5916\u4FA7 + \u961F\u53CB\u5206\u644A",
          ko: "\uB3D9/\uC11C \uC911\uAC04 / ${dir} \uBC14\uAE65 + \uD30C\uD2B8\uB108 \uC250\uC5B4"
        },
        ...Facings.outputStringsCardinalDir
      }
    }),
    whenChant("B425").info("HP to 1"),
    raws({
      id: "M11S Maelstrom Count",
      type: "AddedCombatant",
      netRegex: { name: "Maelstrom", capture: false },
      run: (pull) => pull.maelstromCount = pull.maelstromCount + 1
    }),
    raws({
      id: "M11S Powerful Gust Reminder",
      type: "AddedCombatant",
      netRegex: { name: "Maelstrom", capture: false },
      condition: (pull) => false && pull.maelstromCount === 4,
      infoText: (_pull, _hit, voice) => voice.bait(),
      outputStrings: {
        bait: {
          en: "Bait Gust",
          de: "B\xF6e k\xF6dern",
          cn: "\u8BF1\u5BFC\u5F3A\u98CE",
          ko: "\uAC15\uD48D \uC720\uB3C4"
        }
      }
    }),
    whenChant("B429").hold(6).bigAoe(),
    whenChant("B42B").info("Shared Tank Buster"),
    whenChant("B42F").goSides(),
    raws({
      id: "M11S Meteor",
      type: "HeadMarker",
      netRegex: { id: headSignState["meteor"], capture: true },
      condition: (pull, hit) => {
        if (pull.me === hit.target && pull.phase === "one")
          return true;
        return false;
      },
      response: Response.meteorOnYou(),
      run: (pull) => pull.hasMeteor = true
    }),
    raws({
      id: "M11S Fearsome Fireball",
      type: "HeadMarker",
      netRegex: { id: headSignState["lineStack"], capture: false },
      condition: (pull) => {
        pull.fireballCount = pull.fireballCount + 1;
        return !pull.hasMeteor;
      },
      delaySeconds: 0.1,
      alertText: (pull, _hit, voice) => {
        if (pull.fireballCount === 1) {
          if (pull.role === "tank")
            return voice.wildChargeTank();
          return voice.wildCharge();
        }
        if (pull.role === "tank")
          return voice.tetherBusters();
        return voice.wildChargeMeteor();
      },
      run: (pull) => pull.hasMeteor = false,
      outputStrings: {
        wildCharge: {
          en: "Wild Charge (behind tank)",
          de: "Wilde Rage (hinter einen Tank)",
          cn: "\u6321\u67AA\u5206\u644A (\u5766\u514B\u540E)",
          ko: "\uC9C1\uC120 \uC250\uC5B4 (\uD0F1\uCEE4 \uB4A4\uB85C)"
        },
        wildChargeMeteor: {
          en: "Wild Charge (behind meteor)",
          de: "Wilde Rage (hinter einen Meteor)",
          cn: "\u6321\u67AA\u5206\u644A (\u9668\u77F3\u540E)",
          ko: "\uC9C1\uC120 \uC250\uC5B4 (\uB3CC \uB4A4\uB85C)"
        },
        wildChargeTank: {
          en: "Wild Charge (be in front)",
          de: "Wilde Rage (sei Vorne)",
          cn: "\u6321\u67AA\u5206\u644A (\u4EBA\u7FA4\u524D)",
          ko: "\uC9C1\uC120 \uC250\uC5B4 (\uC55E\uC5D0 \uC788\uAE30)"
        },
        tetherBusters: Voices.tetherBusters
      }
    }),
    raws({
      id: "M11S Meteor Cleanup",
      type: "Ability",
      netRegex: { id: "B435", source: "Comet", capture: true },
      condition: Condition.targetIsYou(),
      run: (pull) => pull.hasMeteor = false
    }),
    whenChant("B43C").alert("LoS behind 3x meteor"),
    whenChant("B43F").info("Short knockback to sides"),
    raws({
      id: "M11S Arena Split Majestic Meteorain Collect",
      type: "MapEffect",
      netRegex: { flags: "00200010", location: ["16", "17"], capture: true },
      condition: (pull) => pull.phase === "arenaSplit",
      run: (pull, hit) => {
        pull.arenaSplitMeteorain = hit.location === "16" ? "westIn" : "westOut";
      }
    }),
    raws({
      id: "M11S Arena Split Majestic Meteowrath Tether Collect",
      type: "Tether",
      netRegex: { id: [headSignState.closeTether, headSignState.farTether], capture: true },
      condition: (pull) => {
        if (pull.phase === "arenaSplit" && pull.arenaSplitTethers.length < 4)
          return true;
        return false;
      },
      preRun: (pull, hit) => pull.arenaSplitTethers.push(hit.target),
      delaySeconds: 0.1,
      run: (pull, hit) => {
        const actor = pull.actorPositions[hit.sourceId];
        const hasLeash = pull.me === hit.target;
        if (actor === void 0) {
          if (hasLeash)
            pull.arenaSplitStretchDirNum = -1;
          return;
        }
        if (hasLeash) {
          const portalFacingDigit = Facings.xyTo4DirIntercardNum(
            actor.x,
            actor.y,
            centers.x,
            centers.y
          );
          const stretchFacingDigit = (portalFacingDigit + 2) % 4;
          pull.arenaSplitStretchDirNum = stretchFacingDigit;
        }
      }
    }),
    raws({
      id: "M11S Arena Split Fire Breath Bait Later",
      type: "Tether",
      netRegex: { id: [headSignState.closeTether, headSignState.farTether], capture: false },
      condition: (pull) => {
        if (pull.phase === "arenaSplit" && pull.arenaSplitTethers.length === 4 && !pull.arenaSplitCalledBait) {
          if (!pull.arenaSplitTethers.includes(pull.me))
            return pull.arenaSplitCalledBait = true;
        }
        return false;
      },
      delaySeconds: 0.1,
      infoText: (_pull, _hit, voice) => voice.fireBreathLater(),
      outputStrings: {
        fireBreathLater: {
          en: "Bait Fire Breath (later)",
          de: "K\xF6der Feueratem (sp\xE4ter)",
          cn: "\u8BF1\u5BFC\u706B\u7130\u5410\u606F (\u7A0D\u540E)",
          ko: "\uD654\uC5FC \uC228\uACB0 \uC720\uB3C4 (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M11S Arena Split Majestic Meteowrath Tether Stretch Later",
      type: "Tether",
      netRegex: { id: [headSignState.closeTether, headSignState.farTether], capture: true },
      condition: (pull, hit) => {
        if (pull.phase === "arenaSplit" && pull.me === hit.target) {
          if (!pull.arenaSplitCalledTether)
            return pull.arenaSplitCalledTether = true;
        }
        return false;
      },
      delaySeconds: 0.1,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === void 0)
          return voice.stretchTetherLater();
        const portalFacingDigit = Facings.xyTo4DirIntercardNum(
          actor.x,
          actor.y,
          centers.y,
          centers.x
        );
        const stretchFacingDigit = (portalFacingDigit + 2) % 4;
        const facingTwo = Facings.outputIntercardDir[stretchFacingDigit];
        return voice.stretchTetherDirLater({ dir: voice[facingTwo ?? "unknown"]() });
      },
      outputStrings: {
        ...Facings.outputStringsIntercardDir,
        stretchTetherDirLater: {
          en: "Tether on YOU: Stretch ${dir} (later)",
          de: "Verbindung auf DIR: Langziehen ${dir} (sp\xE4ter)",
          cn: "\u8FDE\u7EBF\u70B9\u540D: \u5411${dir}\u62C9\u8FDC (\u7A0D\u540E)",
          ko: "\uC120 \uB300\uC0C1\uC790: ${dir}\uCABD\uC73C\uB85C \uB298\uC774\uAE30 (\uB098\uC911\uC5D0)"
        },
        stretchTetherLater: {
          en: "Tether on YOU: Stretch (later)",
          de: "Verbindung auf DIR: Langziehen (sp\xE4ter)",
          cn: "\u8FDE\u7EBF\u70B9\u540D: \u62C9\u8FDC (\u7A0D\u540E)",
          ko: "\uC120 \uB300\uC0C1\uC790: \uB298\uC774\uAE30 (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M11S Explosion Towers",
      type: "StartsUsing",
      netRegex: { id: "B444", source: "The Tyrant", capture: true },
      condition: (pull) => pull.phase === "arenaSplit",
      delaySeconds: (_pull, hit) => parseFloat(hit.castTime) - 6,
      durationSeconds: (_pull, hit) => parseFloat(hit.castTime) - 4,
      suppressSeconds: 1,
      promise: async (pull) => {
        const combatantTwo = (await sayOverlayHandler({
          call: "getCombatants",
          names: [pull.me]
        })).combatants;
        const meBit = combatantTwo[0];
        if (combatantTwo.length !== 1 || meBit === void 0) {
          consol.error(
            `M11S Explosion Towers: Wrong combatants count ${combatantTwo.length}`
          );
          return;
        }
        pull.myPlatform = meBit.PosX < 100 ? "west" : "east";
      },
      alertText: (pull, _hit, voice) => {
        const myPlatforms = pull.myPlatform;
        const facingDigit = pull.arenaSplitStretchDirNum;
        if (facingDigit === 0 || facingDigit === 1) {
          if (myPlatforms === "east") {
            return voice.tetherTowers({
              mech1: voice.northSouthSafe(),
              mech2: voice.avoidFireBreath()
            });
          }
          return voice.tetherTowers({
            mech1: voice.eastSafe(),
            mech2: voice.avoidFireBreath()
          });
        }
        if (facingDigit === 2 || facingDigit === 3) {
          if (myPlatforms === "west") {
            return voice.tetherTowers({
              mech1: voice.northSouthSafe(),
              mech2: voice.avoidFireBreath()
            });
          }
          return voice.tetherTowers({
            mech1: voice.westSafe(),
            mech2: voice.avoidFireBreath()
          });
        }
        if (!pull.arenaSplitTethers.includes(pull.me))
          return voice.fireBreathTowers({
            mech1: voice.northSouthSafe(),
            mech2: voice.baitFireBreath()
          });
        return voice.knockbackTowers();
      },
      outputStrings: {
        knockbackTowers: {
          en: "Get Knockback Towers",
          de: "Nimm R\xFCcksto\xDF-T\xFCrme",
          fr: "Prenez une tour (pouss\xE9e)",
          cn: "\u8E29\u51FB\u98DE\u5854",
          ko: "\uB109\uBC31\uD0D1 \uB4E4\uC5B4\uAC00\uAE30"
        },
        fireBreathTowers: {
          en: "${mech1} => ${mech2}",
          de: "${mech1} => ${mech2}",
          cn: "${mech1} => ${mech2}",
          ko: "${mech1} => ${mech2}"
        },
        tetherTowers: {
          en: "${mech1} => ${mech2}",
          de: "${mech1} => ${mech2}",
          cn: "${mech1} => ${mech2}",
          ko: "${mech1} => ${mech2}"
        },
        baitFireBreath: {
          en: "Bait Near",
          de: "Nahe k\xF6dern",
          cn: "\u9760\u8FD1\u5F15\u5BFC",
          ko: "\uAC00\uAE4C\uC774 \uC720\uB3C4"
        },
        avoidFireBreath: Voices.outOfHitbox,
        northSouthSafe: {
          en: "Tower Knockback to Same Platform",
          de: "Turm-R\xFCcksto\xDF auf die gleiche Plattform",
          cn: "\u88AB\u5854\u51FB\u98DE\u5230\u540C\u4E00\u5E73\u53F0",
          ko: "\uAC19\uC740 \uD50C\uB7AB\uD3FC\uC73C\uB85C \uB109\uBC31"
        },
        eastSafe: {
          en: "Tower Knockback Across to East",
          de: "Turm-R\xFCcksto\xDF Richtung Osten",
          cn: "\u88AB\u5854\u51FB\u98DE\u5230\u53F3\u4FA7\u5E73\u53F0",
          ko: "\uB3D9\uCABD \uD50C\uB7AB\uD3FC\uC73C\uB85C \uB109\uBC31"
        },
        westSafe: {
          en: "Tower Knockback Across to West",
          de: "Turm-R\xFCcksto\xDF Richtung Westen",
          cn: "\u88AB\u5854\u51FB\u98DE\u5230\u5DE6\u4FA7\u5E73\u53F0",
          ko: "\uC11C\uCABD \uD50C\uB7AB\uD3FC\uC73C\uB85C \uB109\uBC31"
        }
      }
    }),
    raws({
      id: "M11S Fire Breath and Bait Puddles",
      type: "HeadMarker",
      netRegex: { id: headSignState["fireBreath"], capture: true },
      condition: (pull, hit) => {
        if (pull.me === hit.target && pull.phase === "arenaSplit")
          return true;
        return false;
      },
      durationSeconds: 6,
      promise: async (pull) => {
        const combatantTwo = (await sayOverlayHandler({
          call: "getCombatants",
          names: [pull.me]
        })).combatants;
        const meBit = combatantTwo[0];
        if (combatantTwo.length !== 1 || meBit === void 0) {
          consol.error(
            `M11S Fire Breath and Bait Puddles: Wrong combatants count ${combatantTwo.length}`
          );
          return;
        }
        pull.myPlatform = meBit.PosX < 100 ? "west" : "east";
      },
      alertText: (pull, _hit, voice) => {
        const meteorains = pull.arenaSplitMeteorain;
        const isWestNear = meteorains === "westIn";
        const myPlatforms = pull.myPlatform;
        if (meteorains !== void 0 && myPlatforms !== void 0) {
          if (myPlatforms === "west") {
            const dir2s = isWestNear ? "front" : "back";
            return voice.fireBreathMechsPlayerWest({
              mech1: voice.fireBreathOnYou(),
              mech2: voice.bait3Puddles(),
              dir: voice[dir2s]()
            });
          }
          const facingTwo = isWestNear ? "back" : "front";
          return voice.fireBreathMechsPlayerEast({
            mech1: voice.fireBreathOnYou(),
            mech2: voice.bait3Puddles(),
            dir: voice[facingTwo]()
          });
        }
        return voice.fireBreathMechs({
          mech1: voice.fireBreathOnYou(),
          mech2: voice.bait3Puddles(),
          mech3: voice.lines()
        });
      },
      outputStrings: {
        bait3Puddles: {
          en: "Bait Puddles x3",
          de: "Fl\xE4chen k\xF6dern x3",
          fr: "D\xE9posez les flaques x3",
          cn: "\u5F15\u5BFC\u5708\u5708 x3",
          ko: "\uC7A5\uD310 \uC720\uB3C4 x3"
        },
        back: {
          en: "Inner Back",
          de: "Innen Hinten",
          cn: "\u5185\u4FA7\u540E",
          ko: "\uC548\uCABD \uB4A4"
        },
        front: {
          en: "Inner Front",
          de: "Innen Vorne",
          cn: "\u5185\u4FA7\u524D",
          ko: "\uC548\uCABD \uC55E"
        },
        lines: {
          en: "Avoid Lines",
          de: "Vermeide Linien",
          fr: "\xC9vitez les lignes",
          ja: "\u76F4\u7DDA\u653B\u6483\u3092\u907F\u3051\u308B",
          cn: "\u8EB2\u907F\u76F4\u7EBF AoE",
          ko: "\uC9C1\uC120\uC7A5\uD310 \uD53C\uD558\uAE30",
          tc: "\u8EB2\u907F\u76F4\u7DDA AoE"
        },
        fireBreathOnYou: {
          en: "Fire Breath on YOU",
          de: "Feueratem auf DIR",
          cn: "\u706B\u7130\u5410\u606F\u70B9\u540D",
          ko: "\uD654\uC5FC \uC228\uACB0 \uB300\uC0C1\uC790"
        },
        fireBreathMechsPlayerWest: {
          en: "${mech1} + ${mech2} => ${dir}",
          de: "${mech1} + ${mech2} => ${dir}",
          cn: "${mech1} + ${mech2} => ${dir}",
          ko: "${mech1} + ${mech2} => ${dir}"
        },
        fireBreathMechsPlayerEast: {
          en: "${mech1} + ${mech2} => ${dir}",
          de: "${mech1} + ${mech2} => ${dir}",
          cn: "${mech1} + ${mech2} => ${dir}",
          ko: "${mech1} + ${mech2} => ${dir}"
        },
        fireBreathMechs: {
          en: "${mech1} + ${mech2} => ${mech3}",
          de: "${mech1} + ${mech2} => ${mech3}",
          cn: "${mech1} + ${mech2} => ${mech3}",
          ko: "${mech1} + ${mech2} => ${mech3}"
        }
      }
    }),
    raws({
      id: "M11S Arena Split Majestic Meteowrath Tether Bait Puddles",
      type: "HeadMarker",
      netRegex: { id: headSignState["fireBreath"], capture: false },
      condition: (pull) => {
        if (pull.phase === "arenaSplit" && pull.arenaSplitTethers.includes(pull.me))
          return true;
        return false;
      },
      durationSeconds: 6,
      suppressSeconds: 1,
      promise: async (pull) => {
        const combatantTwo = (await sayOverlayHandler({
          call: "getCombatants",
          names: [pull.me]
        })).combatants;
        const meBit = combatantTwo[0];
        if (combatantTwo.length !== 1 || meBit === void 0) {
          consol.error(
            `M11S Arena Split Majestic Meteowrath Tether Bait Puddles: Wrong combatants count ${combatantTwo.length}`
          );
          return;
        }
        pull.myPlatform = meBit.PosX < 100 ? "west" : "east";
      },
      alertText: (pull, _hit, voice) => {
        const meteorains = pull.arenaSplitMeteorain;
        const isWestNear = meteorains === "westIn";
        const facingDigit = pull.arenaSplitStretchDirNum;
        const myPlatforms = pull.myPlatform;
        if (facingDigit !== void 0 && myPlatforms !== void 0) {
          const dir1s = Facings.outputIntercardDir[facingDigit] ?? "unknown";
          if (myPlatforms === "west") {
            const dir22s = isWestNear ? "front" : "back";
            return voice.tetherMechsPlayerWest({
              mech1: voice.bait3Puddles(),
              mech2: voice.stretchTetherDir({ dir: voice[dir1s]() }),
              dir: voice[dir22s]()
            });
          }
          const dir2s = isWestNear ? "back" : "front";
          return voice.tetherMechsPlayerEast({
            mech1: voice.bait3Puddles(),
            mech2: voice.stretchTetherDir({ dir: voice[dir1s]() }),
            dir: voice[dir2s]()
          });
        }
        return voice.baitThenStretchMechs({
          mech1: voice.bait3Puddles(),
          mech2: voice.stretchTether(),
          mech3: voice.lines()
        });
      },
      outputStrings: {
        ...Facings.outputStringsIntercardDir,
        bait3Puddles: {
          en: "Bait Puddles x3",
          de: "Fl\xE4chen k\xF6dern x3",
          fr: "D\xE9posez les flaques x3",
          cn: "\u5F15\u5BFC\u5708\u5708 x3",
          ko: "\uC7A5\uD310 \uC720\uB3C4 x3"
        },
        back: {
          en: "Outer Back",
          de: "Au\xDFen Hinten",
          cn: "\u5916\u4FA7\u540E",
          ko: "\uBC14\uAE65\uCABD \uB4A4"
        },
        front: {
          en: "Outer Front",
          de: "Au\xDFen Vorne",
          cn: "\u5916\u4FA7\u524D",
          ko: "\uBC14\uAE65\uCABD \uC55E"
        },
        lines: {
          en: "Avoid Lines",
          de: "Vermeide Linien",
          fr: "\xC9vitez les lignes",
          ja: "\u76F4\u7DDA\u653B\u6483\u3092\u907F\u3051\u308B",
          cn: "\u8EB2\u907F\u76F4\u7EBF AoE",
          ko: "\uC9C1\uC120\uC7A5\uD310 \uD53C\uD558\uAE30",
          tc: "\u8EB2\u907F\u76F4\u7DDA AoE"
        },
        baitThenStretchMechs: {
          en: "${mech1} => ${mech2}  + ${mech3}",
          de: "${mech1} => ${mech2}  + ${mech3}",
          cn: "${mech1} => ${mech2}  + ${mech3}",
          ko: "${mech1} => ${mech2}  + ${mech3}"
        },
        stretchTether: {
          en: "Stretch Tether",
          de: "Verbindung langziehen",
          fr: "\xC9tirez les liens",
          cn: "\u62C9\u8FDC\u8FDE\u7EBF",
          ko: "\uC120 \uB298\uC774\uAE30",
          tc: "\u62C9\u9060\u9023\u7DDA"
        },
        stretchTetherDir: {
          en: "Stretch ${dir}",
          de: "Langiehen ${dir}",
          cn: "\u5411${dir}\u62C9\u8FDC",
          ko: "${dir}\uCABD\uC73C\uB85C \uB298\uC774\uAE30"
        },
        tetherMechsPlayerEast: {
          en: "${mech1} => ${mech2} + ${dir}",
          de: "${mech1} => ${mech2} + ${dir}",
          cn: "${mech1} => ${mech2} + ${dir}",
          ko: "${mech1} => ${mech2} + ${dir}"
        },
        tetherMechsPlayerWest: {
          en: "${mech1} => ${mech2} + ${dir}",
          de: "${mech1} => ${mech2} + ${dir}",
          cn: "${mech1} => ${mech2} + ${dir}",
          ko: "${mech1} => ${mech2} + ${dir}"
        }
      }
    }),
    raws({
      id: "M11S Majestic Meteowrath Tether and Fire Breath Reset",
      type: "Ability",
      netRegex: { id: ["B442", "B443"], source: "The Tyrant", capture: false },
      condition: (pull) => pull.phase === "arenaSplit",
      suppressSeconds: 5,
      run: (pull) => {
        delete pull.arenaSplitMeteorain;
        delete pull.arenaSplitStretchDirNum;
        pull.arenaSplitTethers = [];
        pull.arenaSplitCalledTether = false;
        pull.arenaSplitCalledBait = false;
      }
    }),
    whenSign(headSignState["fiveHitStack"]).cooldown(1).alert("Stack 5x"),
    raws({
      id: "M11S Tower Knockback Direction West",
      type: "StartsUsing",
      netRegex: { id: ["B44E", "B450"], source: "The Tyrant", capture: false },
      run: (pull) => pull.towerKnockbackDir = "west"
    }),
    raws({
      id: "M11S Tower Knockback Direction East",
      type: "StartsUsing",
      netRegex: { id: ["B44A", "B44C"], source: "The Tyrant", capture: false },
      run: (pull) => pull.towerKnockbackDir = "east"
    }),
    raws({
      id: "M11S Arcadion Avalanche Follow Up North Safe",
      type: "StartsUsing",
      netRegex: { id: ["B44B", "B451"], source: "The Tyrant", capture: false },
      priority: true,
      infoText: (pull, _hit, voice) => {
        if (pull.towerKnockbackDir === "east")
          return voice.towerThen({ tower: voice.towerEast(), safe: voice.goNorth() });
        if (pull.towerKnockbackDir === "west")
          return voice.towerThen({ tower: voice.towerWest(), safe: voice.goNorth() });
        return voice.goNorth();
      },
      outputStrings: {
        goNorth: Voices.north,
        towerEast: { en: "Tower to East", de: "Turm nach Osten", cn: "\u53F3\u4FA7\u5854", ko: "\uB3D9\uCABD \uD0D1" },
        towerWest: { en: "Tower to West", de: "Turm nach Westen", cn: "\u5DE6\u4FA7\u5854", ko: "\uC11C\uCABD \uD0D1" },
        towerThen: { en: "${tower} => ${safe}", de: "${tower} => ${safe}", cn: "${tower} => ${safe}", ko: "${tower} => ${safe}" }
      }
    }),
    raws({
      id: "M11S Arcadion Avalanche Follow Up South Safe",
      type: "StartsUsing",
      netRegex: { id: ["B44D", "B44F"], source: "The Tyrant", capture: false },
      priority: true,
      infoText: (pull, _hit, voice) => {
        if (pull.towerKnockbackDir === "east")
          return voice.towerThen({ tower: voice.towerEast(), safe: voice.goSouth() });
        if (pull.towerKnockbackDir === "west")
          return voice.towerThen({ tower: voice.towerWest(), safe: voice.goSouth() });
        return voice.goSouth();
      },
      outputStrings: {
        goSouth: Voices.south,
        towerEast: { en: "Tower to East", de: "Turm nach Osten", cn: "\u53F3\u4FA7\u5854", ko: "\uB3D9\uCABD \uD0D1" },
        towerWest: { en: "Tower to West", de: "Turm nach Westen", cn: "\u5DE6\u4FA7\u5854", ko: "\uC11C\uCABD \uD0D1" },
        towerThen: { en: "${tower} => ${safe}", de: "${tower} => ${safe}", cn: "${tower} => ${safe}", ko: "${tower} => ${safe}" }
      }
    }),
    raws({
      id: "M11S Atomic Impact Collect",
      type: "HeadMarker",
      netRegex: { id: headSignState["atomicImpact"], capture: true },
      condition: Condition.targetIsYou(),
      run: (pull) => pull.hasAtomic = true
    }),
    raws({
      id: "M11S Mammoth Meteor",
      type: "StartsUsingExtra",
      netRegex: { id: "B453", capture: true },
      delaySeconds: 0.1,
      suppressSeconds: 1,
      infoText: (pull, hit, voice) => {
        const meteorXBit = parseFloat(hit.x);
        const meteorYBit = parseFloat(hit.y);
        const meteorQuads = Facings.xyToIntercardDirOutput(meteorXBit, meteorYBit, centers.x, centers.y);
        if (pull.hasAtomic) {
          if (meteorQuads === "dirNE" || meteorQuads === "dirSW")
            return voice.comboDir({ dir1: voice.nw(), dir2: voice.se() });
          return voice.comboDir({ dir1: voice.ne(), dir2: voice.sw() });
        }
        return voice.getMiddle();
      },
      outputStrings: {
        nw: Voices.dirNW,
        ne: Voices.dirNE,
        sw: Voices.dirSW,
        se: Voices.dirSE,
        comboDir: {
          en: "Go ${dir1}/${dir2} => Bait Impacts, Avoid Corners",
          de: "Geh ${dir1}/${dir2} => K\xF6der Impakts, Ecken vermeiden",
          cn: "\u53BB${dir1}/${dir2} => \u5F15\u5BFC\u706B\u5708, \u8EB2\u907F\u89D2\u843D",
          ko: "${dir1}/${dir2} \uC774\uB3D9 => \uC7A5\uD310 \uC720\uB3C4, \uAD6C\uC11D \uD53C\uD558\uAE30"
        },
        getMiddle: {
          en: "Proximity AoE; Get Middle => Bait Puddles",
          de: "Distanz-AoE; Geh in die Mitte => Fl\xE4chen k\xF6dern",
          cn: "\u9760\u8FD1AoE; \u53BB\u4E2D\u95F4 => \u5F15\u5BFC\u5708\u5708",
          ko: "\uAC70\uB9AC\uAC10\uC1E0 \uC9D5; \uC911\uC559\uC73C\uB85C => \uC7A5\uD310 \uC720\uB3C4"
        }
      }
    }),
    whenChant("B456").when((c) => !c.data.hasAtomic).cooldown(1).towers(),
    raws({
      id: "M11S Ecliptic Stampede Majestic Meteowrath Tether Collect",
      type: "Tether",
      netRegex: { id: [headSignState.closeTether, headSignState.farTether], capture: true },
      condition: (pull, hit) => {
        if (pull.me === hit.target && pull.phase === "ecliptic")
          return true;
        return false;
      },
      suppressSeconds: 9999,
      run: (pull) => pull.hadEclipticTether = true
    }),
    raws({
      id: "M11S Ecliptic Stampede Majestic Meteowrath Tethers",
      type: "Tether",
      netRegex: { id: [headSignState.closeTether, headSignState.farTether], capture: true },
      condition: (pull, hit) => {
        if (pull.me === hit.target && pull.phase === "ecliptic")
          return true;
        return false;
      },
      suppressSeconds: 9999,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === void 0)
          return;
        const portalFacingDigit = Facings.xyTo8DirNum(actor.x, actor.y, centers.x, centers.y);
        const stretchFacingDigit = pull.triggerSetConfig.majesticMeteowrathTetherDir === "ccw" ? (portalFacingDigit + 3) % 8 : (portalFacingDigit + 5) % 8;
        const stretchFacing = Facings.output8Dir[stretchFacingDigit] ?? "unknown";
        return voice.stretchTetherDir({ dir: voice[stretchFacing]() });
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        stretchTetherDir: {
          en: "Stretch Tether ${dir}",
          de: "Verbindungen langziehen ${dir}",
          cn: "\u5411${dir}\u62C9\u7EBF",
          ko: "${dir}\uCABD\uC73C\uB85C \uC120 \uB298\uC774\uAE30"
        }
      }
    }),
    raws({
      id: "M11S Two-way Fireball",
      type: "StartsUsing",
      netRegex: { id: "B7BD", source: "The Tyrant", capture: false },
      alertText: (pull, _hit, voice) => {
        const lureFacing = pull.triggerSetConfig.twoWayFireballBaitDir === "ns" ? "northSouth" : "eastWest";
        if (pull.hadEclipticTether)
          return voice.twoWayBehind({ dir: voice[lureFacing]() });
        return voice.twoWayFront({ dir: voice[lureFacing]() });
      },
      outputStrings: {
        eastWest: {
          en: "East/West",
          de: "Osten/Westen",
          fr: "Est/Ouest",
          cn: "\u5DE6/\u53F3",
          ko: "\uB3D9/\uC11C",
          tc: "\u6771/\u897F"
        },
        northSouth: {
          en: "North/South",
          de: "Norden/S\xFCden",
          fr: "Nord/Sud",
          cn: "\u4E0A/\u4E0B",
          ko: "\uBD81/\uB0A8",
          tc: "\u5317/\u5357"
        },
        twoWayFront: {
          en: "${dir} Line Stack, Be in Front",
          de: "${dir} in einer Linie Sammeln, sei vorne",
          cn: "${dir}\u5411\u76F4\u7EBF\u5206\u644A\uFF0C\u7AD9\u524D\u65B9",
          ko: "${dir} \uC9C1\uC120 \uC250\uC5B4, \uC55E\uC5D0 \uC788\uAE30"
        },
        twoWayBehind: {
          en: "Move; ${dir} Line Stack, Get behind",
          de: "Geh ${dir}, in einer Linie Sammeln, sei hinten",
          cn: "\u79FB\u52A8; ${dir}\u5411\u76F4\u7EBF\u5206\u644A\uFF0C\u7AD9\u540E\u65B9",
          ko: "\uC774\uB3D9; ${dir} \uC9C1\uC120 \uC250\uC5B4, \uB4A4\uB85C \uAC00\uAE30"
        }
      }
    }),
    raws({
      id: "M11S Four-way Fireball",
      type: "StartsUsing",
      netRegex: { id: "B45A", source: "The Tyrant", capture: false },
      alertText: (pull, _hit, voice) => {
        if (pull.hadEclipticTether)
          return voice.fourWayBehind();
        return voice.fourWayFront();
      },
      outputStrings: {
        fourWayFront: {
          en: "Intercardinal Line Stack, Be in Front",
          de: "Interkardinal in einer Linie sammeln, sei vorne",
          cn: "\u56DB\u89D2\u5206\u644A, \u7AD9\u524D\u65B9",
          ko: "\uB300\uAC01\uC120 \uC250\uC5B4, \uC55E\uC5D0 \uC788\uAE30"
        },
        fourWayBehind: {
          en: "Intercardinal Line Stack, Get behind",
          de: "Interkardinal in einer Linie sammeln, sei hinten",
          cn: "\u56DB\u89D2\u5206\u644A, \u7AD9\u540E\u65B9",
          ko: "\uB300\uAC01\uC120 \uC250\uC5B4, \uB4A4\uB85C \uAC00\uAE30"
        }
      }
    }),
    raws({
      id: "M11S Heartbreaker (Enrage Sequence)",
      type: "StartsUsing",
      netRegex: { id: "B45D", source: "The Tyrant", capture: false },
      preRun: (pull) => pull.heartbreakerCount = pull.heartbreakerCount + 1,
      infoText: (pull, _hit, voice) => {
        switch (pull.heartbreakerCount) {
          case 1:
            return voice.heartbreaker1({
              tower: voice.getTower(),
              stack: voice.stack5x()
            });
          case 2:
            return voice.heartbreaker2({
              tower: voice.getTower(),
              stack: voice.stack6x()
            });
          case 3:
            return voice.heartbreaker3({
              tower: voice.getTower(),
              stack: voice.stack7x()
            });
        }
      },
      outputStrings: {
        getTower: {
          en: "Get Tower",
          de: "Turm nehmen",
          fr: "Prenez la tour",
          ja: "\u5854\u3092\u8E0F\u3080",
          cn: "\u8E29\u5854",
          ko: "\uD0D1 \uBC1F\uAE30",
          tc: "\u8E29\u5854"
        },
        stack5x: {
          en: "Stack 5x",
          de: "5x Sammeln",
          fr: "5x Packages",
          ja: "\u982D\u5272\u308A\uFF15\u56DE",
          cn: "5\u8FDE\u5206\u644A",
          ko: "\uC250\uC5B4 5\uBC88",
          tc: "5\u9023\u5206\u6524"
        },
        stack6x: {
          en: "Stack 6x",
          de: "Sammeln 6x",
          cn: "6\u8FDE\u5206\u644A",
          ko: "\uC250\uC5B4 6\uBC88"
        },
        stack7x: {
          en: "Stack 7x",
          de: "Sammeln 7x",
          cn: "7\u8FDE\u5206\u644A",
          ko: "\uC250\uC5B4 7\uBC88"
        },
        heartbreaker1: {
          en: "${tower} => ${stack}",
          de: "${tower} => ${stack}",
          cn: "${tower} => ${stack}",
          ko: "${tower} => ${stack}"
        },
        heartbreaker2: {
          en: "${tower} => ${stack}",
          de: "${tower} => ${stack}",
          cn: "${tower} => ${stack}",
          ko: "${tower} => ${stack}"
        },
        heartbreaker3: {
          en: "${tower} => ${stack}",
          de: "${tower} => ${stack}",
          cn: "${tower} => ${stack}",
          ko: "${tower} => ${stack}"
        }
      }
    })
  ]
});
