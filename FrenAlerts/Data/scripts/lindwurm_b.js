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
const rep2FacingSpotTable = {
  "north": 0,
  "south": 1,
  "northeast": 2,
  "southwest": 3,
  "east": 4,
  "west": 5,
  "southeast": 6,
  "northwest": 7
};
const replication2VoiceWords = {
  getTether: {
    en: "Get Tether",
    de: "Nimm Verbindung",
    cn: "\u63A5\u7EBF",
    ko: "\uC120 \uAC00\uC838\uAC00\uAE30"
  },
  getBossTether: {
    en: "Get Boss Tether",
    de: "Nimm Boss-Verbindung",
    cn: "\u63A5 BOSS \u7EBF",
    ko: "\uBCF4\uC2A4 \uC120 \uAC00\uC838\uAC00\uAE30"
  },
  getConeTetherCW: {
    en: "Get Clockwise Cone Tether",
    de: "Nimm Kegel-Verbindung im Uhrzeigersinn",
    cn: "\u987A\u65F6\u9488\u63A5\u6247\u5F62\u7EBF",
    ko: "\uC2DC\uACC4\uBC29\uD5A5 \uBD80\uCC44\uAF34 \uC120 \uAC00\uC838\uAC00\uAE30"
  },
  getConeTetherCCW: {
    en: "Get Counterclock Cone Tether",
    de: "Nimm Kegel-Verbindung gegen den Uhrzeigersinn",
    cn: "\u9006\u65F6\u9488\u63A5\u6247\u5F62\u7EBF",
    ko: "\uBC18\uC2DC\uACC4\uBC29\uD5A5 \uBD80\uCC44\uAF34 \uC120 \uAC00\uC838\uAC00\uAE30"
  },
  getStackTetherCW: {
    en: "Get Clockwise Stack Tether",
    de: "Nimm Sammel-Verbindung im Uhrzeigersinn",
    cn: "\u987A\u65F6\u9488\u63A5\u5206\u644A\u7EBF",
    ko: "\uC2DC\uACC4\uBC29\uD5A5 \uC250\uC5B4\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
  },
  getStackTetherCCW: {
    en: "Get Counterclock Stack Tether",
    de: "Nimm Sammel-Verbindung gegen den Uhrzeigersinn",
    cn: "\u9006\u65F6\u9488\u63A5\u5206\u644A\u7EBF",
    ko: "\uBC18\uC2DC\uACC4\uBC29\uD5A5 \uC250\uC5B4\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
  },
  getDefamationTetherCW: {
    en: "Get Clockwise Defamation Tether",
    de: "Nimm Ehrenstrafe-Verbindung im Uhrzeigersinn",
    cn: "\u987A\u65F6\u9488\u63A5\u5927\u5708\u7EBF",
    ko: "\uC2DC\uACC4\uBC29\uD5A5 \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
  },
  getDefamationTetherCCW: {
    en: "Get Counterclock Defamation Tether",
    de: "Nimm Ehrenstrafe-Verbindung gegen den Uhrzeigersinn",
    cn: "\u9006\u65F6\u9488\u63A5\u5927\u5708\u7EBF",
    ko: "\uBC18\uC2DC\uACC4\uBC29\uD5A5 \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
  },
  getNoTether: {
    en: "Get Nothing",
    de: "Nichts nehmen",
    cn: "\u4E0D\u63A5\u7EBF",
    ko: "\uC544\uBB34\uAC83\uB3C4 \uAC00\uC838\uAC00\uC9C0 \uC54A\uAE30"
  },
  getTetherNClone: {
    en: "${tether}",
    de: "${tether}",
    cn: "${tether}",
    ko: "${tether}"
  },
  getTetherNEClone: {
    en: "${tether}",
    de: "${tether}",
    cn: "${tether}",
    ko: "${tether}"
  },
  getTetherEClone: {
    en: "${tether}",
    de: "${tether}",
    cn: "${tether}",
    ko: "${tether}"
  },
  getTetherSEClone: {
    en: "${tether}",
    de: "${tether}",
    cn: "${tether}",
    ko: "${tether}"
  },
  getTetherSClone: {
    en: "${tether}",
    de: "${tether}",
    cn: "${tether}",
    ko: "${tether}"
  },
  getTetherSWClone: {
    en: "${tether}",
    de: "${tether}",
    cn: "${tether}",
    ko: "${tether}"
  },
  getTetherWClone: {
    en: "${tether}",
    de: "${tether}",
    cn: "${tether}",
    ko: "${tether}"
  },
  getTetherNWClone: {
    en: "${tether}",
    de: "${tether}",
    cn: "${tether}",
    ko: "${tether}"
  }
};

defineDuty({
  id: "AacHeavyweightM4SavageP2",
  name: "M12S PT2 - Lindwurm",
  category: "Savage \u2013 Dawntrail",
  zoneId: 1327,
  boss: "Lindwurm",
  center: { x: 100, y: 100 },
  state: {
    phase: "doorboss",
    mortalSlayerGreenLeft: 0,
    mortalSlayerGreenRight: 0,
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
      }
    }),
    raws({
      id: "M12S Phase Two Staging Tracker",
      type: "AddedCombatant",
      netRegex: { name: "Understudy", capture: false },
      condition: (pull) => pull.phase === "replication1",
      run: (pull) => pull.phase = "replication2"
    }),
    raws({
      id: "M12S Phase Two Replication Tracker",
      type: "StartsUsing",
      netRegex: { id: "B4D8", source: "Lindwurm", capture: false },
      run: (pull) => {
        if (pull.replicationCounter === 0)
          pull.phase = "replication1";
        pull.replicationCounter = pull.replicationCounter + 1;
      }
    }),
    raws({
      id: "M12S Phase Two Boss ID Collect",
      type: "StartsUsing",
      netRegex: { id: "B4E1", source: "Lindwurm", capture: true },
      condition: (pull) => pull.phase === "replication2",
      suppressSeconds: 9999,
      run: (pull, hit) => pull.replication2BossId = hit.sourceId
    }),
    raws({
      id: "M12S Phase Two Reenactment Tracker",
      type: "StartsUsing",
      netRegex: { id: "B4EC", source: "Lindwurm", capture: false },
      run: (pull) => {
        if (pull.phase === "replication2") {
          pull.phase = "reenactment1";
          return;
        }
        pull.phase = "reenactment2";
      }
    }),
    raws({
      id: "M12S Phase Two Twisted Vision Tracker",
      type: "StartsUsing",
      netRegex: { id: "BBE2", source: "Lindwurm", capture: false },
      run: (pull) => {
        pull.twistedVisionCounter = pull.twistedVisionCounter + 1;
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
    whenChant("B528").alert("big AoE!"),
    raws({
      id: "M12S Winged Scourge",
      type: "StartsUsing",
      netRegex: { id: ["B4DA", "B4DB"], source: "Lindschrat", capture: true },
      suppressSeconds: 1,
      infoText: (pull, hit, voice) => {
        if (hit.id === "B4DA") {
          if (pull.replication1FollowUp)
            return voice.northSouthCleaves2();
          const x2Bit = parseFloat(hit.x);
          if (x2Bit < 87 || x2Bit > 113)
            return voice.eWCleavingNorthSouth();
          return voice.nSCleavingNorthSouth();
        }
        if (pull.replication1FollowUp)
          return voice.eastWestCleaves2();
        const x = parseFloat(hit.x);
        if (x < 87 || x > 113)
          return voice.eWCleavingEastWest();
        return voice.nSCleavingEastWest();
      },
      outputStrings: {
        nSCleavingNorthSouth: {
          en: "N/S Cleaving North/South",
          de: "N/S Cleaven Norden/S\xFCden",
          cn: "\u4E0A/\u4E0B\u6247\u5F62 \u4E0A/\u4E0B",
          ko: "\uBD81/\uB0A8 \uBD80\uCC44\uAF34 \uBD81\uCABD/\uB0A8\uCABD"
        },
        eWCleavingNorthSouth: {
          en: "E/W Cleaving North/South",
          de: "O/W Cleaven Norden/S\xFCden",
          cn: "\u5DE6/\u53F3\u6247\u5F62 \u4E0A/\u4E0B",
          ko: "\uB3D9/\uC11C \uBD80\uCC44\uAF34 \uBD81\uCABD/\uB0A8\uCABD"
        },
        nSCleavingEastWest: {
          en: "N/S Cleaving East/West",
          de: "N/S Cleaven Osten/Westen",
          cn: "\u4E0A/\u4E0B\u6247\u5F62 \u5DE6/\u53F3",
          ko: "\uBD81/\uB0A8 \uBD80\uCC44\uAF34 \uB3D9\uCABD/\uC11C\uCABD"
        },
        eWCleavingEastWest: {
          en: "E/W Cleaving East/West",
          de: "O/W Cleaven Osten/Westen",
          cn: "\u5DE6/\u53F3\u6247\u5F62 \u5DE6/\u53F3",
          ko: "\uB3D9/\uC11C \uBD80\uCC44\uAF34 \uB3D9\uCABD/\uC11C\uCABD"
        },
        northSouthCleaves2: {
          en: "North/South Cleaves",
          de: "Nord/S\xFCd Cleaves",
          cn: "\u4E0A/\u4E0B\u6247\u5F62",
          ko: "\uBD81/\uB0A8 \uBD80\uCC44\uAF34"
        },
        eastWestCleaves2: {
          en: "East/West Cleaves",
          de: "Ost/West Cleaves",
          cn: "\u5DE6/\u53F3\u6247\u5F62",
          ko: "\uB3D9/\uC11C \uBD80\uCC44\uAF34"
        }
      }
    }),
    raws({
      id: "M12S Fire and Dark Resistance Down II Collector",
      type: "GainsEffect",
      netRegex: { effectId: ["CFB", "B79"], capture: true },
      condition: (pull) => !pull.replication1FollowUp,
      run: (pull, hit) => {
        const aura = hit.effectId === "CFB" ? "dark" : "fire";
        if (pull.me === hit.target)
          pull.replication1Debuff = aura;
        if (aura === "fire")
          pull.replication1FireDebuffCounter = pull.replication1FireDebuffCounter + 1;
        else
          pull.replication1DarkDebuffCounter = pull.replication1DarkDebuffCounter + 1;
      }
    }),
    raws({
      id: "M12S Fire and Dark Resistance Down II",
      type: "GainsEffect",
      netRegex: { effectId: ["CFB", "B79"], capture: true },
      condition: (pull, hit) => {
        if (pull.me === hit.target)
          return !pull.replication1FollowUp;
        return false;
      },
      suppressSeconds: 9999,
      infoText: (_pull, hit, voice) => {
        return hit.effectId === "CFB" ? voice.dark() : voice.fire();
      },
      outputStrings: {
        fire: {
          en: "Fire Debuff: Spread near Dark (later)",
          de: "Feuer Debuff: Nahe Dunkel verteilen (sp\xE4ter)",
          cn: "\u706B Debuff: \u6697\u9644\u8FD1\u5206\u6563 (\u7A0D\u540E)",
          ko: "\uBD88 \uB514\uBC84\uD504: \uC5B4\uB460 \uADFC\uCC98 \uC0B0\uAC1C (\uB098\uC911\uC5D0)"
        },
        dark: {
          en: "Dark Debuff: Stack near Fire (later)",
          de: "Dunkel Debuff: Nahe Feuer sammeln (sp\xE4ter)",
          cn: "\u6697 Debuff: \u706B\u9644\u8FD1\u5206\u644A (\u7A0D\u540E)",
          ko: "\uC5B4\uB460 \uB514\uBC84\uD504: \uBD88 \uADFC\uCC98 \uC250\uC5B4 (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M12S Fake Fire Resistance Down II",
      type: "GainsEffect",
      netRegex: { effectId: ["CFB", "B79"], capture: false },
      condition: (pull) => !pull.replication1FollowUp,
      delaySeconds: 1.3,
      suppressSeconds: 9999,
      infoText: (pull, _hit, voice) => {
        if (pull.replication1Debuff === void 0) {
          if (pull.replication1FireDebuffCounter === 2 && pull.replication1DarkDebuffCounter === 4)
            return voice.noDebuff();
          return undefined;
        }
      },
      outputStrings: {
        noDebuff: {
          en: "No Debuff: Spread near Dark (later)",
          de: "Kein Debuff: Nahe Dunkel verteilen (sp\xE4ter)",
          cn: "\u65E0 Debuff: \u6697\u9644\u8FD1\u5206\u6563 (\u7A0D\u540E)",
          ko: "\uB514\uBC84\uD504 \uC5C6\uC74C: \uC5B4\uB460 \uADFC\uCC98 \uC0B0\uAC1C (\uB098\uC911\uC5D0)"
        },
        noDebuffFail: {
          en: "Debuffs Messed Up, Check Partner",
          de: "Debuffs durcheinander, Partner \xFCberpr\xFCfen",
          cn: "Debuff \u83B7\u53D6\u6545\u969C, \u68C0\u67E5\u642D\u6863\u72B6\u6001",
          ko: "\uB514\uBC84\uD504 \uAF2C\uC784, \uD30C\uD2B8\uB108 \uD655\uC778"
        }
      }
    }),
    raws({
      id: "M12S Snaking Kick",
      type: "StartsUsing",
      netRegex: { id: "B527", source: "Lindwurm", capture: true },
      delaySeconds: 0.1,
      suppressSeconds: 9999,
      alertText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === void 0)
          return voice.getBehind();
        const facingDigit = (Facings.hdgTo16DirNum(actor.heading) + 8) % 16;
        const facingTwo = Facings.output16Dir[facingDigit] ?? "unknown";
        return voice.getBehindDir({
          dir: voice[facingTwo](),
          mech: voice.getBehind()
        });
      },
      outputStrings: {
        ...Facings.outputStrings16Dir,
        getBehind: Voices.getBehind,
        getBehindDir: {
          en: "${dir}: ${mech}",
          de: "${dir}: ${mech}",
          cn: "${dir}: ${mech}",
          ko: "${dir}: ${mech}"
        }
      }
    }),
    raws({
      id: "M12S Replication 1 Follow-up Tracker",
      type: "Ability",
      netRegex: { id: "B527", source: "Lindwurm", capture: false },
      suppressSeconds: 9999,
      run: (pull) => pull.replication1FollowUp = true
    }),
    raws({
      id: "M12S Top-Tier Slam Actor Collect",
      type: "Ability",
      netRegex: { id: "B4D9", source: "Lindschrat", capture: true },
      condition: (pull, hit) => {
        if (pull.replication1FollowUp) {
          const place = pull.actorPositions[hit.sourceId];
          if (place === void 0)
            return false;
          const xFilters = place.x % 1;
          const yFilters = place.y % 1;
          if (xFilters === 0 && yFilters === 0 && place.heading === 0)
            return false;
          return true;
        }
        return false;
      },
      suppressSeconds: 9999,
      run: (pull, hit) => pull.replication1FireActor = hit.sourceId
    }),
    raws({
      id: "M12S Top-Tier Slam/Mighty Magic Locations",
      type: "Ability",
      netRegex: { id: "B4D9", source: "Lindschrat", capture: false },
      condition: (pull) => {
        if (pull.replication1FollowUp && pull.replication1FireActor !== void 0)
          return true;
        return false;
      },
      delaySeconds: 1,
      suppressSeconds: 9999,
      infoText: (pull, _hit, voice) => {
        const fireCode = pull.replication1FireActor;
        if (fireCode === void 0)
          return;
        const actor = pull.actorPositions[fireCode];
        if (actor === void 0)
          return;
        const aura = pull.replication1Debuff;
        const x = actor.x;
        const facingDigit = Facings.xyTo8DirNum(x, actor.y, centers.x, centers.y);
        const dir1s = Facings.output8Dir[facingDigit] ?? "unknown";
        const facingNum2 = (facingDigit + 4) % 8;
        const dir2s = Facings.output8Dir[facingNum2] ?? "unknown";
        const isNearTwo = x > 94 && x < 106;
        const fireNear = isNearTwo ? dir1s : dir2s;
        const fireAway = isNearTwo ? dir2s : dir1s;
        if (aura === "dark")
          return voice.fire({
            dir1: voice[fireNear](),
            dir2: voice[fireAway]()
          });
        const darkNear = isNearTwo ? dir2s : dir1s;
        const darkAway = isNearTwo ? dir1s : dir2s;
        if (aura === "fire" || pull.replication1FireDebuffCounter === 2 && pull.replication1DarkDebuffCounter === 4)
          return voice.dark({
            dir1: voice[darkNear](),
            dir2: voice[darkAway]()
          });
        return voice.darkDebuffFail({
          dir1: voice[darkNear](),
          dir2: voice[darkAway]()
        });
      },
      outputStrings: {
        ...Facings.outputStringsIntercardDir,
        fire: {
          en: "Bait Fire In ${dir1}/Out ${dir2} (Partners)",
          de: "K\xF6der Feuer im ${dir1}/Au\xDFen ${dir2} (Partner)",
          cn: "\u5185${dir1}/\u5916${dir2}\u8BF1\u5BFC\u706B (\u548C\u642D\u6863\u4E00\u8D77)",
          ko: "\uBD88 \uC548\uCABD ${dir1}/\uBC14\uAE65\uCABD ${dir2} \uC720\uB3C4 (\uD30C\uD2B8\uB108)"
        },
        dark: {
          en: "Bait Dark In ${dir1}/Out ${dir2} (Solo)",
          de: "K\xF6der Dunkel in ${dir1}/Au\xDFen ${dir2} (Solo)",
          cn: "\u5185${dir1}/\u5916${dir2}\u8BF1\u5BFC\u6697 (\u5355\u72EC)",
          ko: "\uC5B4\uB460 \uC548\uCABD ${dir1}/\uBC14\uAE65\uCABD ${dir2} \uC720\uB3C4 (\uD63C\uC790)"
        },
        darkDebuffFail: {
          en: "Check Partner, Dark is In ${dir1}/Out ${dir2}",
          de: "Partner \xFCberpr\xFCfen, Dunkel ist ${dir1}/Au\xDFen ${dir2}",
          cn: "\u68C0\u67E5\u642D\u6863\u72B6\u6001, \u6697\u5728\u5185${dir1}/\u5916${dir2}",
          ko: "\uD30C\uD2B8\uB108 \uD655\uC778, \uC5B4\uB460 \uC548\uCABD ${dir1}/\uBC14\uAE65\uCABD ${dir2}"
        }
      }
    }),
    raws({
      id: "M12S Double Sobat",
      type: "HeadMarker",
      netRegex: { id: headSignState["sharedTankbuster"], capture: true },
      response: Response.sharedTankBuster()
    }),
    raws({
      id: "M12S Double Sobat 2",
      type: "Ability",
      netRegex: { id: ["B521", "B522", "B523", "B524"], source: "Lindwurm", capture: true },
      suppressSeconds: 1,
      alertText: (_pull, hit, voice) => {
        const x = parseFloat(hit.x);
        const y = parseFloat(hit.y);
        const markX = parseFloat(hit.targetX);
        const markY = parseFloat(hit.targetY);
        const facingDigit = Facings.xyTo16DirNum(markX, markY, x, y);
        const readNewFacingDigit = (facingNum2, id) => {
          switch (id) {
            case "B521":
              return facingNum2;
            case "B522":
              return facingNum2 - 4;
            case "B523":
              return facingNum2 - 8;
            case "B524":
              return facingNum2 - 12;
          }
          throw new UnreachableCod();
        };
        const newFacingDigit = (readNewFacingDigit(facingDigit, hit.id) + 16 + 8) % 16;
        const facingTwo = Facings.output16Dir[newFacingDigit] ?? "unknown";
        return voice.getBehindDir({
          dir: voice[facingTwo](),
          mech: voice.getBehind()
        });
      },
      outputStrings: {
        ...Facings.outputStrings16Dir,
        getBehind: Voices.getBehind,
        getBehindDir: {
          en: "${dir}: ${mech}",
          de: "${dir}: ${mech}",
          cn: "${dir}: ${mech}",
          ko: "${dir}: ${mech}"
        }
      }
    }),
    raws({
      id: "M12S Esoteric Finisher",
      type: "StartsUsing",
      netRegex: { id: "B525", source: "Lindwurm", capture: true },
      delaySeconds: (_pull, hit) => parseFloat(hit.castTime),
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          tankBusterCleaves: Voices.tankBusterCleaves,
          avoidTankCleaves: Voices.avoidTankCleaves
        };
        if (pull.role === "tank" || pull.role === "healer") {
          if (pull.role === "healer")
            return { infoText: voice.tankBusterCleaves() };
          return { alertText: voice.tankBusterCleaves() };
        }
        return { infoText: voice.avoidTankCleaves() };
      }
    }),
    raws({
      id: "M12S Staging 1 Tethered Clone Collect",
      type: "Tether",
      netRegex: { id: headSignState["lockedTether"], capture: true },
      condition: (pull) => pull.replicationCounter === 1,
      run: (pull, hit) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === void 0)
          return;
        const facingDigit = Facings.xyTo8DirNum(actor.x, actor.y, centers.x, centers.y);
        pull.replication2CloneDirNumPlayers[facingDigit] = hit.target;
      }
    }),
    raws({
      id: "M12S Staging 1 Tethered Clone",
      type: "Tether",
      netRegex: { id: headSignState["lockedTether"], capture: true },
      condition: Condition.targetIsYou(),
      suppressSeconds: 9999,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === void 0)
          return voice.cloneTether();
        const facingDigit = Facings.xyTo8DirNum(actor.x, actor.y, centers.x, centers.y);
        const facingTwo = Facings.output8Dir[facingDigit] ?? "unknown";
        return voice.cloneTetherDir({ dir: voice[facingTwo]() });
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        cloneTether: {
          en: "Tethered to Clone",
          de: "Verbunden zum Klon",
          cn: "\u5206\u8EAB\u8FDE\u7EBF",
          ko: "\uBD84\uC2E0\uACFC \uC5F0\uACB0\uB428"
        },
        cloneTetherDir: {
          en: "Tethered to ${dir} Clone",
          de: "Verbunden zum ${dir} Klon",
          cn: "\u4E0E${dir}\u5206\u8EAB\u8FDE\u7EBF",
          ko: "${dir} \uBD84\uC2E0\uACFC \uC5F0\uACB0\uB428"
        }
      }
    }),
    raws({
      id: "M12S Replication 2 and Replication 4 Ability Tethers Collect",
      type: "Tether",
      netRegex: {
        id: [
          headSignState["projectionTether"],
          headSignState["manaBurstTether"],
          headSignState["heavySlamTether"],
          headSignState["fireballSplashTether"]
        ],
        capture: true
      },
      condition: (pull) => {
        if (pull.phase === "replication2" || pull.phase === "idyllic")
          return true;
        return false;
      },
      run: (pull, hit) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === void 0)
          return;
        const facingDigit = Facings.xyTo8DirNum(actor.x, actor.y, centers.x, centers.y);
        if (pull.phase === "replication2") {
          if (hit.id !== headSignState["fireballSplashTether"])
            pull.replication2DirNumAbility[facingDigit] = hit.id;
        }
        if (pull.phase === "idyllic")
          pull.replication4DirNumAbility[facingDigit] = hit.id;
      }
    }),
    raws({
      id: "M12S Replication 2 Ability Tethers Initial Call",
      type: "Tether",
      netRegex: {
        id: [
          headSignState["projectionTether"],
          headSignState["manaBurstTether"],
          headSignState["heavySlamTether"],
          headSignState["fireballSplashTether"]
        ],
        capture: false
      },
      suppressSeconds: 9999,
      infoText: (pull, _hit, voice) => {
        const clone = pull.replication2CloneDirNumPlayers;
        const strats = pull.triggerSetConfig.replication2Strategy;
        const myFacingDigit = Object.keys(clone).find(
          (tagTwo) => clone[parseInt(tagTwo)] === pull.me
        );
        if (myFacingDigit !== void 0) {
          switch (parseInt(myFacingDigit)) {
            case 0:
              return voice.getTetherNClone({
                tether: strats === "dn" ? voice.getBossTether() : strats === "banana" ? voice.getConeTetherCW() : strats === "nukemaru" ? voice.getConeTetherCCW() : voice.getTether()
              });
            case 1:
              return voice.getTetherNEClone({
                tether: strats === "dn" ? voice.getConeTetherCW() : strats === "banana" ? voice.getDefamationTetherCW() : strats === "nukemaru" ? voice.getStackTetherCCW() : voice.getTether()
              });
            case 2:
              return voice.getTetherEClone({
                tether: strats === "dn" ? voice.getStackTetherCW() : strats === "banana" ? voice.getNoTether() : strats === "nukemaru" ? voice.getBossTether() : voice.getTether()
              });
            case 3:
              return voice.getTetherSEClone({
                tether: strats === "dn" ? voice.getDefamationTetherCW() : strats === "banana" ? voice.getDefamationTetherCCW() : strats === "nukemaru" ? voice.getStackTetherCW() : voice.getTether()
              });
            case 4:
              return voice.getTetherSClone({
                tether: strats === "dn" ? voice.getNoTether() : strats === "banana" ? voice.getConeTetherCCW() : strats === "nukemaru" ? voice.getConeTetherCW() : voice.getTether()
              });
            case 5:
              return voice.getTetherSWClone({
                tether: strats === "dn" ? voice.getDefamationTetherCCW() : strats === "banana" ? voice.getStackTetherCCW() : strats === "nukemaru" ? voice.getDefamationTetherCW() : voice.getTether()
              });
            case 6:
              return voice.getTetherWClone({
                tether: strats === "dn" ? voice.getStackTetherCCW() : strats === "banana" ? voice.getBossTether() : strats === "nukemaru" ? voice.getNoTether() : voice.getTether()
              });
            case 7:
              return voice.getTetherNWClone({
                tether: strats === "dn" ? voice.getConeTetherCCW() : strats === "banana" ? voice.getStackTetherCW() : strats === "nukemaru" ? voice.getDefamationTetherCCW() : voice.getTether()
              });
          }
        }
        return voice.getTether();
      },
      outputStrings: replication2VoiceWords
    }),
    raws({
      id: "M12S Replication 2 Locked Tether Collect",
      type: "Tether",
      netRegex: { id: headSignState["lockedTether"], capture: true },
      condition: (pull) => {
        if (pull.phase === "replication2" && pull.replicationCounter === 2)
          return true;
        return false;
      },
      run: (pull, hit) => {
        const markThree = hit.target;
        const casterCode = hit.sourceId;
        const bigTwo = headSignState["fireballSplashTether"];
        if (pull.replication2BossId === casterCode)
          pull.replication2PlayerAbilities[markThree] = bigTwo;
        else if (pull.replication2BossId !== casterCode) {
          const actor = pull.actorPositions[casterCode];
          if (actor === void 0) {
            pull.replication2PlayerAbilities[markThree] = "unknown";
            return;
          }
          const facingDigit = Facings.xyTo8DirNum(
            actor.x,
            actor.y,
            centers.x,
            centers.y
          );
          const skill = pull.replication2DirNumAbility[facingDigit];
          if (skill === void 0) {
            pull.replication2PlayerAbilities[markThree] = "unknown";
            return;
          }
          pull.replication2PlayerAbilities[markThree] = skill;
        }
        if (Object.keys(pull.replication2PlayerAbilities).length === 7) {
          if (pull.replication2PlayerAbilities[pull.me] === void 0)
            pull.replication2PlayerAbilities[pull.me] = "none";
          const abilitie = pull.replication2PlayerAbilities;
          const sequence = [0, 4, 1, 5, 2, 6, 3, 7];
          const members = pull.replication2CloneDirNumPlayers;
          for (const facingDigit of sequence) {
            const player = members[facingDigit] ?? "unknown";
            const skill = abilitie[player] ?? "none";
            pull.replication2PlayerOrder.push(player);
            pull.replication2AbilityOrder.push(skill);
          }
          const detectStrategies = (order2s) => {
            const slur = headSignState["manaBurstTether"];
            const stack = headSignState["heavySlamTether"];
            const projections = headSignState["projectionTether"];
            if (order2s[rep2FacingSpotTable["north"]] === bigTwo && order2s[rep2FacingSpotTable["south"]] === "none" && order2s[rep2FacingSpotTable["northeast"]] === projections && order2s[rep2FacingSpotTable["southwest"]] === slur && order2s[rep2FacingSpotTable["east"]] === stack && order2s[rep2FacingSpotTable["west"]] === stack && order2s[rep2FacingSpotTable["southeast"]] === slur)
              return "dn";
            if (order2s[rep2FacingSpotTable["north"]] === projections && order2s[rep2FacingSpotTable["south"]] === projections && order2s[rep2FacingSpotTable["northeast"]] === slur && order2s[rep2FacingSpotTable["southwest"]] === stack && order2s[rep2FacingSpotTable["east"]] === "none" && order2s[rep2FacingSpotTable["west"]] === bigTwo && order2s[rep2FacingSpotTable["southeast"]] === slur)
              return "banana";
            if (order2s[rep2FacingSpotTable["north"]] === projections && order2s[rep2FacingSpotTable["south"]] === projections && order2s[rep2FacingSpotTable["northeast"]] === stack && order2s[rep2FacingSpotTable["southwest"]] === slur && order2s[rep2FacingSpotTable["east"]] === bigTwo && order2s[rep2FacingSpotTable["west"]] === "none" && order2s[rep2FacingSpotTable["southeast"]] === stack)
              return "nukemaru";
            return "unknown";
          };
          pull.replication2StrategyDetected = detectStrategies(pull.replication2AbilityOrder);
        }
      }
    }),
    raws({
      id: "M12S Replication 2 Locked Tether",
      type: "Tether",
      netRegex: { id: headSignState["lockedTether"], capture: true },
      condition: (pull, hit) => {
        if (pull.phase === "replication2" && pull.replicationCounter === 2 && pull.me === hit.target)
          return true;
        return false;
      },
      delaySeconds: 0.1,
      infoText: (pull, hit, voice) => {
        const casterCode = hit.sourceId;
        const strats = pull.triggerSetConfig.replication2Strategy;
        if (pull.replication2BossId === casterCode)
          return voice.fireballSplashTether({
            mech1: strats === "dn" ? voice.baitJumpDNN({ strat: voice.north() }) : strats === "banana" ? voice.baitJumpBananaW({ strat: voice.west() }) : strats === "nukemaru" ? voice.baitJumpNukemaruE({ strat: voice.east() }) : voice.baitJump()
          });
        const actor = pull.actorPositions[casterCode];
        const skill = pull.replication2PlayerAbilities[pull.me];
        const clone = pull.replication2CloneDirNumPlayers;
        const myFacingDigit = Object.keys(clone).find(
          (tagTwo) => clone[parseInt(tagTwo)] === pull.me
        );
        const myFacingDigitInt = myFacingDigit === void 0 ? -1 : parseInt(myFacingDigit);
        if (actor === void 0) {
          switch (skill) {
            case headSignState["projectionTether"]:
              switch (myFacingDigitInt) {
                case 0:
                  return voice.projectionTether({
                    mech1: strats === "banana" ? voice.baitProteanBananaN({
                      strat: voice["dirWSW"]()
                    }) : strats === "nukemaru" ? voice.baitProteanNukemaruN({
                      strat: voice["dirENE"]()
                    }) : voice.baitProtean()
                  });
                case 1:
                  return voice.projectionTether({
                    mech1: strats === "dn" ? voice.baitProteanDNNE({ strat: voice.north() }) : voice.baitProtean()
                  });
                case 4:
                  return voice.projectionTether({
                    mech1: strats === "banana" ? voice.baitProteanBananaS({
                      strat: voice["dirWNW"]()
                    }) : strats === "nukemaru" ? voice.baitProteanNukemaruN({
                      strat: voice["dirESE"]()
                    }) : voice.baitProtean()
                  });
                case 7:
                  return voice.projectionTether({
                    mech1: strats === "dn" ? voice.baitProteanDNNW({ strat: voice.north() }) : voice.baitProtean()
                  });
              }
              return voice.projectionTether({
                mech1: strats === "dn" ? voice.baitProteanDN({ strat: voice.north() }) : strats === "banana" ? voice.baitProteanBanana({ strat: voice.west() }) : strats === "nukemaru" ? voice.baitProteanNukemaru({ strat: voice.east() }) : voice.baitProtean()
              });
            case headSignState["manaBurstTether"]:
              switch (myFacingDigitInt) {
                case 1:
                  return voice.manaBurstTether({
                    mech1: strats === "banana" ? voice.defamationOnYouBananaNE({
                      strat: voice["dirNNE"]()
                    }) : voice.defamationOnYou()
                  });
                case 3:
                  return voice.manaBurstTether({
                    mech1: strats === "dn" ? voice.defamationOnYouDNSE({
                      strat: voice["dirESE"]()
                    }) : strats === "banana" ? voice.defamationOnYouBananaSE({
                      strat: voice["dirSSE"]()
                    }) : voice.defamationOnYou()
                  });
                case 5:
                  return voice.manaBurstTether({
                    mech1: strats === "dn" ? voice.defamationOnYouDNSW({
                      strat: voice["dirWSW"]()
                    }) : strats === "nukemaru" ? voice.defamationOnYouNukemaruSW({
                      strat: voice["dirSSW"]()
                    }) : voice.defamationOnYou()
                  });
                case 7:
                  return voice.manaBurstTether({
                    mech1: strats === "nukemaru" ? voice.defamationOnYouNukemaruNW({
                      strat: voice["dirNNW"]()
                    }) : voice.defamationOnYou()
                  });
              }
              return voice.manaBurstTether({
                mech1: voice.defamationOnYou()
              });
            case headSignState["heavySlamTether"]:
              switch (myFacingDigitInt) {
                case 1:
                  return voice.heavySlamTether({
                    mech1: strats === "nukemaru" ? voice.baitProteanNukemaruNE({ strat: voice.east() }) : voice.baitProtean()
                  });
                case 2:
                  return voice.heavySlamTether({
                    mech1: strats === "dn" ? voice.baitProteanDNE({
                      strat: voice["dirNNE"]()
                    }) : voice.baitProtean()
                  });
                case 3:
                  return voice.heavySlamTether({
                    mech1: strats === "nukemaru" ? voice.baitProteanNukemaruSE({ strat: voice.east() }) : voice.baitProtean()
                  });
                case 5:
                  return voice.heavySlamTether({
                    mech1: strats === "banana" ? voice.baitProteanBananaSW({ strat: voice.west() }) : voice.baitProtean()
                  });
                case 6:
                  return voice.heavySlamTether({
                    mech1: strats === "dn" ? voice.baitProteanDNW({
                      strat: voice["dirNNW"]()
                    }) : voice.baitProtean()
                  });
                case 7:
                  return voice.heavySlamTether({
                    mech1: strats === "banana" ? voice.baitProteanBananaNW({ strat: voice.west() }) : voice.baitProtean()
                  });
              }
              return voice.heavySlamTether({
                mech1: strats === "dn" ? voice.baitProteanDN({ strat: voice.north() }) : strats === "banana" ? voice.baitProteanBanana({ strat: voice.west() }) : voice.baitProtean()
              });
          }
          return;
        }
        const facingDigit = Facings.xyTo8DirNum(
          actor.x,
          actor.y,
          centers.x,
          centers.y
        );
        const facingTwo = Facings.output8Dir[facingDigit] ?? "unknown";
        switch (skill) {
          case headSignState["projectionTether"]:
            switch (myFacingDigitInt) {
              case 0:
                return voice.projectionTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "banana" ? voice.baitProteanBananaN({
                    strat: voice["dirWSW"]()
                  }) : strats === "nukemaru" ? voice.baitProteanNukemaruN({
                    strat: voice["dirENE"]()
                  }) : voice.baitProtean()
                });
              case 1:
                return voice.projectionTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "dn" ? voice.baitProteanDNNE({ strat: voice.north() }) : voice.baitProtean()
                });
              case 4:
                return voice.projectionTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "banana" ? voice.baitProteanBananaS({
                    strat: voice["dirWNW"]()
                  }) : strats === "nukemaru" ? voice.baitProteanNukemaruS({
                    strat: voice["dirESE"]()
                  }) : voice.baitProtean()
                });
              case 7:
                return voice.projectionTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "dn" ? voice.baitProteanDNNW({ strat: voice.north() }) : voice.baitProtean()
                });
            }
            return voice.projectionTetherDir({
              dir: voice[facingTwo](),
              mech1: strats === "dn" ? voice.baitProteanDN({ strat: voice.north() }) : strats === "banana" ? voice.baitProteanBanana({ strat: voice.west() }) : strats === "nukemaru" ? voice.baitProteanNukemaru({ strat: voice.east() }) : voice.baitProtean()
            });
          case headSignState["manaBurstTether"]:
            switch (myFacingDigitInt) {
              case 1:
                return voice.manaBurstTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "banana" ? voice.defamationOnYouBananaNE({
                    strat: voice["dirNNE"]()
                  }) : voice.defamationOnYou()
                });
              case 3:
                return voice.manaBurstTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "dn" ? voice.defamationOnYouDNSE({
                    strat: voice["dirESE"]()
                  }) : strats === "banana" ? voice.defamationOnYouBananaSE({
                    strat: voice["dirSSE"]()
                  }) : voice.defamationOnYou()
                });
              case 5:
                return voice.manaBurstTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "dn" ? voice.defamationOnYouDNSW({
                    strat: voice["dirWSW"]()
                  }) : strats === "nukemaru" ? voice.defamationOnYouNukemaruSW({
                    strat: voice["dirSSW"]()
                  }) : voice.defamationOnYou()
                });
              case 7:
                return voice.manaBurstTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "nukemaru" ? voice.defamationOnYouNukemaruNW({
                    strat: voice["dirNNW"]()
                  }) : voice.defamationOnYou()
                });
            }
            return voice.manaBurstTetherDir({
              dir: voice[facingTwo](),
              mech1: voice.defamationOnYou()
            });
          case headSignState["heavySlamTether"]:
            switch (myFacingDigitInt) {
              case 1:
                return voice.heavySlamTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "nukemaru" ? voice.baitProteanNukemaruNE({ strat: voice.east() }) : voice.baitProtean()
                });
              case 2:
                return voice.heavySlamTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "dn" ? voice.baitProteanDNE({
                    strat: voice["dirNNE"]()
                  }) : voice.baitProtean()
                });
              case 3:
                return voice.heavySlamTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "nukemaru" ? voice.baitProteanNukemaruSE({ strat: voice.east() }) : voice.baitProtean()
                });
              case 5:
                return voice.heavySlamTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "banana" ? voice.baitProteanBananaSW({ strat: voice.west() }) : voice.baitProtean()
                });
              case 6:
                return voice.heavySlamTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "dn" ? voice.baitProteanDNW({
                    strat: voice["dirNNW"]()
                  }) : voice.baitProtean()
                });
              case 7:
                return voice.heavySlamTetherDir({
                  dir: voice[facingTwo](),
                  mech1: strats === "banana" ? voice.baitProteanBananaNW({ strat: voice.west() }) : voice.baitProtean()
                });
            }
            return voice.heavySlamTetherDir({
              dir: voice[facingTwo](),
              mech1: strats === "dn" ? voice.baitProteanDN({ strat: voice.north() }) : strats === "banana" ? voice.baitProteanBanana({ strat: voice.west() }) : strats === "nukemaru" ? voice.baitProteanBanana({ strat: voice.east() }) : voice.baitProtean()
            });
        }
      },
      outputStrings: {
        ...Facings.outputStrings16Dir,
        north: Voices.north,
        east: Voices.east,
        south: Voices.south,
        west: Voices.west,
        defamationOnYou: Voices.defamationOnYou,
        defamationOnYouDNSE: {
          en: "Defamation on YOU, Go ${strat}",
          de: "Ehrenstrafe auf DIR, Geh ${strat}",
          cn: "\u5927\u5708\u70B9\u540D, \u53BB${strat}",
          ko: "\uAD11\uC5ED\uC9D5 \uB300\uC0C1\uC790, ${strat}"
        },
        defamationOnYouDNSW: {
          en: "Defamation on YOU, Go ${strat}",
          de: "Ehrenstrafe auf DIR, Geh ${strat}",
          cn: "\u5927\u5708\u70B9\u540D, \u53BB${strat}",
          ko: "\uAD11\uC5ED\uC9D5 \uB300\uC0C1\uC790, ${strat}"
        },
        defamationOnYouBananaNE: {
          en: "Defamation on YOU, Go ${strat}",
          de: "Ehrenstrafe auf DIR, Geh ${strat}",
          cn: "\u5927\u5708\u70B9\u540D, \u53BB${strat}",
          ko: "\uAD11\uC5ED\uC9D5 \uB300\uC0C1\uC790, ${strat}"
        },
        defamationOnYouBananaSE: {
          en: "Defamation on YOU, Go ${strat}",
          de: "Ehrenstrafe auf DIR, Geh ${strat}",
          cn: "\u5927\u5708\u70B9\u540D, \u53BB${strat}",
          ko: "\uAD11\uC5ED\uC9D5 \uB300\uC0C1\uC790, ${strat}"
        },
        defamationOnYouNukemaruSW: {
          en: "Defamation on YOU, Go ${strat}",
          de: "Ehrenstrafe auf DIR, Geh ${strat}",
          cn: "\u5927\u5708\u70B9\u540D, \u53BB${strat}",
          ko: "\uAD11\uC5ED\uC9D5 \uB300\uC0C1\uC790, ${strat}"
        },
        defamationOnYouNukemaruNW: {
          en: "Defamation on YOU, Go ${strat}",
          de: "Ehrenstrafe auf DIR, Geh ${strat}",
          cn: "\u5927\u5708\u70B9\u540D, \u53BB${strat}",
          ko: "\uAD11\uC5ED\uC9D5 \uB300\uC0C1\uC790, ${strat}"
        },
        baitProtean: {
          en: "Bait Protean from Boss",
          de: "K\xF6der Kegel-AoE vom Boss",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4"
        },
        baitProteanDN: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanDNNE: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanDNE: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanDNW: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanDNNW: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanBanana: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanBananaN: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanBananaS: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanBananaSW: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanBananaNW: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanNukemaru: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanNukemaruN: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanNukemaruS: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanNukemaruNE: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitProteanNukemaruSE: {
          en: "Bait Protean from Boss (${strat})",
          de: "K\xF6der Kegel-AoE vom Boss (${strat})",
          cn: "\u4ECE Boss \u8BF1\u5BFC\u6247\u5F62 (${strat})",
          ko: "\uBCF4\uC2A4\uC758 \uBD80\uCC44\uAF34 \uC720\uB3C4 (${strat})"
        },
        baitJump: {
          en: "Bait Jump",
          de: "K\xF6der Sprung",
          cn: "\u8BF1\u5BFC\u8DF3\u8DC3",
          ko: "\uC810\uD504 \uC720\uB3C4"
        },
        baitJumpDNN: {
          en: "Bait Jump ${strat}",
          de: "K\xF6der Sprung ${strat}",
          cn: "\u8BF1\u5BFC\u8DF3\u8DC3 ${strat}",
          ko: "\uC810\uD504 \uC720\uB3C4 ${strat}"
        },
        baitJumpBananaW: {
          en: "Bait Jump ${strat}",
          de: "K\xF6der Sprung ${strat}",
          cn: "\u8BF1\u5BFC\u8DF3\u8DC3 ${strat}",
          ko: "\uC810\uD504 \uC720\uB3C4 ${strat}"
        },
        baitJumpNukemaruE: {
          en: "Bait Jump ${strat}",
          de: "K\xF6der Sprung ${strat}",
          cn: "\u8BF1\u5BFC\u8DF3\u8DC3 ${strat}",
          ko: "\uC810\uD504 \uC720\uB3C4 ${strat}"
        },
        projectionTetherDir: {
          en: "${dir} Cone Tether: ${mech1}",
          de: "${dir} Kegel-Verbindung: ${mech1}",
          cn: "${dir} \u6247\u5F62\u8FDE\u7EBF: ${mech1}",
          ko: "${dir} \uBD80\uCC44\uAF34 \uC120: ${mech1}"
        },
        projectionTether: {
          en: "Cone Tether: ${mech1}",
          de: "Kegel-Verbindung: ${mech1}",
          cn: "\u6247\u5F62\u8FDE\u7EBF: ${mech1}",
          ko: "\uBD80\uCC44\uAF34 \uC120: ${mech1}"
        },
        manaBurstTetherDir: {
          en: "${dir} Defamation Tether: ${mech1}",
          de: "${dir} Ehrenstrafe-Verbindung: ${mech1}",
          cn: "${dir} \u5927\u5708\u8FDE\u7EBF: ${mech1}",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120: ${mech1}"
        },
        manaBurstTether: {
          en: "Defamation Tether: ${mech1}",
          de: "Ehrenstrafe-Verbindung: ${mech1}",
          cn: "\u5927\u5708\u8FDE\u7EBF: ${mech1}",
          ko: "\uAD11\uC5ED\uC9D5 \uC120: ${mech1}"
        },
        heavySlamTetherDir: {
          en: "${dir} Stack Tether: ${mech1}",
          de: "${dir} Sammel-Verbindung: ${mech1}",
          cn: "${dir} \u5206\u644A\u8FDE\u7EBF: ${mech1}",
          ko: "${dir} \uC250\uC5B4\uC9D5 \uC120: ${mech1}"
        },
        heavySlamTether: {
          en: "Stack Tether: ${mech1}",
          de: "Sammel-Verbindung: ${mech1}",
          cn: "\u5206\u644A\u8FDE\u7EBF: ${mech1}",
          ko: "\uC250\uC5B4\uC9D5 \uC120: ${mech1}"
        },
        fireballSplashTether: {
          en: "Boss Tether: ${mech1}",
          de: "Boss-Verbindung: ${mech1}",
          cn: "Boss \u8FDE\u7EBF: ${mech1}",
          ko: "\uBCF4\uC2A4 \uC120: ${mech1}"
        }
      }
    }),
    raws({
      id: "M12S Replication 2 Mana Burst Far Target",
      type: "Tether",
      netRegex: { id: headSignState["lockedTether"], capture: false },
      condition: (pull) => {
        if (pull.phase === "replication2" && pull.replicationCounter === 2)
          return true;
        return false;
      },
      delaySeconds: 0.2,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const skill = pull.replication2PlayerAbilities[pull.me];
        const strats = pull.triggerSetConfig.replication2Strategy;
        if (skill !== "none" || skill === void 0)
          return;
        return voice.noTether({
          mech1: strats === "dn" ? voice.baitFarDefamationDN({ strat: voice.south() }) : strats === "banana" ? voice.baitFarDefamationBanana({ strat: voice.east() }) : strats === "nukemaru" ? voice.baitFarDefamationNukemaru({ strat: voice.west() }) : voice.baitFarDefamation(),
          mech2: voice.stackGroups()
        });
      },
      outputStrings: {
        east: Voices.east,
        south: Voices.south,
        west: Voices.west,
        baitFarDefamation: {
          en: "Bait Far Defamation",
          de: "K\xF6der Entfernte Ehrenstrafe",
          cn: "\u8BF1\u5BFC\u8FDC\u5927\u5708",
          ko: "\uBA40\uB9AC \uAD11\uC5ED\uC9D5 \uC720\uB3C4"
        },
        baitFarDefamationDN: {
          en: "Bait Far Defamation (Go ${strat})",
          de: "K\xF6der Entfernte Ehrenstrafe (Geh ${strat})",
          cn: "\u8BF1\u5BFC\u8FDC\u5927\u5708 (\u53BB ${strat})",
          ko: "\uBA40\uB9AC \uAD11\uC5ED\uC9D5 \uC720\uB3C4 (${strat})"
        },
        baitFarDefamationBanana: {
          en: "Bait Far Defamation (Go ${strat})",
          de: "K\xF6der Entfernte Ehrenstrafe (Geh ${strat})",
          cn: "\u8BF1\u5BFC\u8FDC\u5927\u5708 (\u53BB ${strat})",
          ko: "\uBA40\uB9AC \uAD11\uC5ED\uC9D5 \uC720\uB3C4 (${strat})"
        },
        baitFarDefamationNukemaru: {
          en: "Bait Far Defamation (Go ${strat})",
          de: "K\xF6der Entfernte Ehrenstrafe (Geh ${strat})",
          cn: "\u8BF1\u5BFC\u8FDC\u5927\u5708 (\u53BB ${strat})",
          ko: "\uBA40\uB9AC \uAD11\uC5ED\uC9D5 \uC720\uB3C4 (${strat})"
        },
        stackGroups: {
          en: "Stack Groups",
          de: "Gruppen-Sammeln",
          fr: "Package en groupes",
          ja: "\u7D44\u307F\u5206\u3051\u982D\u5272\u308A",
          cn: "\u5206\u7EC4\u5206\u644A",
          ko: "\uADF8\uB8F9\uBCC4 \uC250\uC5B4",
          tc: "\u5206\u7D44\u5206\u6524"
        },
        noTether: {
          en: "No Tether: ${mech1} => ${mech2}",
          de: "Keine Verbindung: ${mech1} => ${mech2}",
          cn: "\u65E0\u8FDE\u7EBF: ${mech1} => ${mech2}",
          ko: "\uC120 \uC5C6\uC74C: ${mech1} => ${mech2}"
        }
      }
    }),
    raws({
      id: "M12S Heavy Slam",
      type: "Ability",
      netRegex: { id: "B4E7", source: "Lindwurm", capture: false },
      suppressSeconds: 1,
      alertText: (pull, _hit, voice) => {
        const skill = pull.replication2PlayerAbilities[pull.me];
        switch (skill) {
          case headSignState["projectionTether"]:
            return voice.projectionTether({
              mech1: voice.stackGroups(),
              mech2: voice.lookAway(),
              mech3: voice.getBehind()
            });
          case headSignState["manaBurstTether"]:
            return voice.manaBurstTether({
              mech1: voice.stackGroups(),
              mech2: voice.projection(),
              mech3: voice.getBehind()
            });
          case headSignState["heavySlamTether"]:
            return voice.heavySlamTether({
              mech1: voice.stackGroups(),
              mech2: voice.projection(),
              mech3: voice.getBehind()
            });
          case headSignState["fireballSplashTether"]:
            return voice.fireballSplashTether({
              mech1: voice.stackGroups(),
              mech2: voice.projection(),
              mech3: voice.getBehind()
            });
        }
        return voice.noTether({
          mech1: voice.stackGroups(),
          mech2: voice.projection(),
          mech3: voice.getBehind()
        });
      },
      outputStrings: {
        getBehind: Voices.getBehind,
        lookAway: Voices.lookAway,
        projection: {
          en: "Cones",
          de: "Klone",
          cn: "\u6247\u5F62",
          ko: "\uBD80\uCC44\uAF34"
        },
        stackGroups: {
          en: "Stack Groups",
          de: "Gruppen-Sammeln",
          fr: "Package en groupes",
          ja: "\u7D44\u307F\u5206\u3051\u982D\u5272\u308A",
          cn: "\u5206\u7EC4\u5206\u644A",
          ko: "\uADF8\uB8F9\uBCC4 \uC250\uC5B4",
          tc: "\u5206\u7D44\u5206\u6524"
        },
        stackOnYou: Voices.stackOnYou,
        projectionTether: {
          en: "${mech1} + ${mech2} => ${mech3}",
          de: "${mech1} + ${mech2} => ${mech3}",
          cn: "${mech1} + ${mech2} => ${mech3}",
          ko: "${mech1} + ${mech2} => ${mech3}"
        },
        manaBurstTether: {
          en: "${mech1} => ${mech2} => ${mech3}",
          de: "${mech1} => ${mech2} => ${mech3}",
          cn: "${mech1} => ${mech2} => ${mech3}",
          ko: "${mech1} => ${mech2} => ${mech3}"
        },
        heavySlamTether: {
          en: "${mech1} => ${mech2} => ${mech3}",
          de: "${mech1} => ${mech2} => ${mech3}",
          cn: "${mech1} => ${mech2} => ${mech3}",
          ko: "${mech1} => ${mech2} => ${mech3}"
        },
        fireballSplashTether: {
          en: "${mech1} => ${mech2} => ${mech3}",
          de: "${mech1} => ${mech2} => ${mech3}",
          cn: "${mech1} => ${mech2} => ${mech3}",
          ko: "${mech1} => ${mech2} => ${mech3}"
        },
        noTether: {
          en: "${mech1} => ${mech2} => ${mech3}",
          de: "${mech1} => ${mech2} => ${mech3}",
          cn: "${mech1} => ${mech2} => ${mech3}",
          ko: "${mech1} => ${mech2} => ${mech3}"
        }
      }
    }),
    raws({
      id: "M12S Grotesquerie",
      type: "Ability",
      netRegex: { id: "B4EA", source: "Lindwurm", capture: false },
      suppressSeconds: 9999,
      alertText: (pull, _hit, voice) => {
        const bigCode = pull.replication2BossId;
        if (bigCode === void 0)
          return voice.getBehind();
        const actor = pull.actorPositions[bigCode];
        if (actor === void 0)
          return voice.getBehind();
        const facingDigit = (Facings.hdgTo16DirNum(actor.heading) + 8) % 16;
        const facingTwo = Facings.output16Dir[facingDigit] ?? "unknown";
        return voice.getBehindDir({
          dir: voice[facingTwo](),
          mech: voice.getBehind()
        });
      },
      outputStrings: {
        ...Facings.outputStrings16Dir,
        getBehind: Voices.getBehind,
        getBehindDir: {
          en: "${dir}: ${mech}",
          de: "${dir}: ${mech}",
          cn: "${dir}: ${mech}",
          ko: "${dir}: ${mech}"
        }
      }
    }),
    raws({
      id: "M12S Netherwrath Near/Far and First Clones",
      type: "StartsUsing",
      netRegex: { id: ["B52E", "B52F"], source: "Lindwurm", capture: true },
      infoText: (pull, hit, voice) => {
        const strats = pull.replication2StrategyDetected;
        const skill = pull.replication2PlayerAbilities[pull.me];
        const isNears = hit.id === "B52E";
        if (strats === "dn") {
          if (isNears) {
            switch (skill) {
              case headSignState["projectionTether"]:
                return voice.projectionTetherNear({
                  proteanBaits: voice.beFar(),
                  mech1: voice.scaldingWave(),
                  mech2: voice.stacks(),
                  spiteBaits: voice.near()
                });
              case headSignState["manaBurstTether"]:
                return voice.manaBurstTetherNear({
                  spiteBaits: voice.beNear(),
                  mech1: voice.timelessSpite(),
                  mech2: voice.proteans(),
                  proteanBaits: voice.far()
                });
              case headSignState["heavySlamTether"]:
                return voice.heavySlamTetherNear({
                  proteanBaits: voice.beFar(),
                  mech1: voice.scaldingWave(),
                  mech2: voice.stacks(),
                  spiteBaits: voice.near()
                });
              case headSignState["fireballSplashTether"]:
                return voice.fireballSplashTetherNear({
                  spiteBaits: voice.beNear(),
                  mech1: voice.timelessSpite(),
                  mech2: voice.proteans(),
                  proteanBaits: voice.far()
                });
            }
            return voice.noTetherNear({
              spiteBaits: voice.beNear(),
              mech1: voice.timelessSpite(),
              mech2: voice.proteans(),
              proteanBaits: voice.far()
            });
          }
          switch (skill) {
            case headSignState["projectionTether"]:
              return voice.projectionTetherFar({
                proteanBaits: voice.beNear(),
                mech1: voice.scaldingWave(),
                mech2: voice.stacks(),
                spiteBaits: voice.far()
              });
            case headSignState["manaBurstTether"]:
              return voice.manaBurstTetherFar({
                spiteBaits: voice.beFar(),
                mech1: voice.timelessSpite(),
                mech2: voice.proteans(),
                proteanBaits: voice.near()
              });
            case headSignState["heavySlamTether"]:
              return voice.heavySlamTetherFar({
                proteanBaits: voice.beNear(),
                mech1: voice.scaldingWave(),
                mech2: voice.stacks(),
                spiteBaits: voice.far()
              });
            case headSignState["fireballSplashTether"]:
              return voice.fireballSplashTetherFar({
                spiteBaits: voice.beFar(),
                mech1: voice.timelessSpite(),
                mech2: voice.proteans(),
                proteanBaits: voice.near()
              });
          }
          return voice.noTetherFar({
            spiteBaits: voice.beFar(),
            mech1: voice.timelessSpite(),
            mech2: voice.proteans(),
            proteanBaits: voice.near()
          });
        }
        if (strats === "banana" || strats === "nukemaru") {
          switch (skill) {
            case headSignState["projectionTether"]:
              return voice.projectionTetherBait({
                mech1: voice.timelessSpite(),
                spiteBaits: isNears ? voice.near() : voice.far(),
                mech2: voice.proteans()
              });
            case headSignState["manaBurstTether"]:
              return voice.manaBurstTetherHitbox({
                mech1: strats === "banana" ? voice.hitboxBanana() : voice.hitboxNukemaru(),
                spiteBaits: isNears ? voice.near() : voice.far(),
                mech2: voice.stackDir({
                  dir: strats === "banana" ? voice.dirSW() : voice.dirNE()
                })
              });
            case headSignState["heavySlamTether"]:
              return voice.heavySlamTetherBait({
                mech1: voice.timelessSpite(),
                spiteBaits: isNears ? voice.near() : voice.far(),
                mech2: voice.proteans()
              });
            case headSignState["fireballSplashTether"]:
              return voice.fireballSplashTetherHitbox({
                mech1: strats === "banana" ? voice.hitboxBanana() : voice.hitboxNukemaru(),
                spiteBaits: isNears ? voice.near() : voice.far(),
                mech2: voice.stackDir({
                  dir: strats === "banana" ? voice.dirSW() : voice.dirNE()
                })
              });
          }
          return voice.noTetherHitbox({
            mech1: strats === "banana" ? voice.hitboxBanana() : voice.hitboxNukemaru(),
            spiteBaits: isNears ? voice.near() : voice.far(),
            mech2: voice.stackDir({
              dir: strats === "banana" ? voice.dirSW() : voice.dirNE()
            })
          });
        }
        const readMechanic = (order2s) => {
          const bigTwo = headSignState["fireballSplashTether"];
          const slur = headSignState["manaBurstTether"];
          const stack = headSignState["heavySlamTether"];
          const projections = headSignState["projectionTether"];
          if (order2s === bigTwo)
            return "proteans";
          if (order2s === slur || order2s === "none")
            return "defamation";
          if (order2s === projections)
            return "projection";
          if (order2s === stack)
            return "stack";
          return "unknown";
        };
        const sequence = pull.replication2AbilityOrder;
        const mechanic1s = readMechanic(sequence[0] ?? "unknown");
        const mechanic2s = readMechanic(sequence[1] ?? "unknown");
        const mechanic3s = readMechanic(sequence[2] ?? "unknown");
        const mechanic4s = readMechanic(sequence[3] ?? "unknown");
        return voice.netherwrathMechThenMech({
          spiteBaits: isNears ? voice.near() : voice.far(),
          mech1: voice[mechanic1s](),
          mech2: voice[mechanic2s](),
          mech3: voice[mechanic3s](),
          mech4: voice[mechanic4s]()
        });
      },
      outputStrings: {
        dirNE: Voices.dirNE,
        dirSW: Voices.dirSW,
        scaldingWave: Voices.protean,
        timelessSpite: Voices.stackPartner,
        stacks: Voices.stacks,
        stackDir: {
          en: "Stack ${dir}",
          de: "Sammeln ${dir}",
          cn: "${dir} \u5206\u644A",
          ko: "\uC250\uC5B4 ${dir}"
        },
        proteans: {
          en: "Proteans",
          de: "Kegel-AoEs",
          cn: "\u6247\u5F62",
          ko: "\uBD80\uCC44\uAF34"
        },
        beNear: {
          en: "Be Near",
          de: "Sei Nahe",
          cn: "\u7AD9\u8FD1",
          ko: "\uAC00\uAE4C\uC774 \uC788\uAE30"
        },
        beFar: {
          en: "Be Far",
          de: "Sei Fern",
          cn: "\u7AD9\u8FDC",
          ko: "\uBA40\uB9AC \uC788\uAE30"
        },
        hitboxBanana: {
          en: "Be West on Boss Hitbox",
          de: "Sei westlich der Hitbox vom Boss",
          cn: "\u53BB\u5DE6\u8FB9, Boss\u5224\u5B9A\u5708\u4E0A",
          ko: "\uBCF4\uC2A4 \uD788\uD2B8\uBC15\uC2A4 \uC11C\uCABD\uC5D0 \uC788\uAE30"
        },
        hitboxNukemaru: {
          en: "Be West on Boss Hitbox",
          de: "Sei westlich auf der Hitbox vom Boss",
          cn: "\u53BB\u5DE6\u8FB9, Boss\u5224\u5B9A\u5708\u4E0B",
          ko: "\uBCF4\uC2A4 \uD788\uD2B8\uBC15\uC2A4 \uC11C\uCABD\uC5D0 \uC788\uAE30"
        },
        near: {
          en: "Near",
          de: "Nah",
          fr: "Proche",
          cn: "\u8FD1",
          ko: "\uAC00\uAE4C\uC774"
        },
        far: {
          en: "Far",
          de: "Fern",
          fr: "Loin",
          cn: "\u8FDC",
          ko: "\uBA40\uB9AC"
        },
        projectionTetherFar: {
          en: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          de: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          cn: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          ko: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})"
        },
        manaBurstTetherFar: {
          en: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          de: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          cn: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          ko: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})"
        },
        heavySlamTetherFar: {
          en: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          de: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          cn: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          ko: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})"
        },
        fireballSplashTetherFar: {
          en: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          de: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          cn: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          ko: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})"
        },
        noTetherFar: {
          en: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          de: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          cn: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          ko: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})"
        },
        projectionTetherNear: {
          en: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          de: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          cn: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          ko: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})"
        },
        manaBurstTetherNear: {
          en: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          de: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          cn: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          ko: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})"
        },
        heavySlamTetherNear: {
          en: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          de: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          cn: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})",
          ko: "${proteanBaits} + ${mech1} (${mech2} ${spiteBaits})"
        },
        fireballSplashTetherNear: {
          en: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          de: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          cn: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          ko: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})"
        },
        noTetherNear: {
          en: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          de: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          cn: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})",
          ko: "${spiteBaits} + ${mech1} (${mech2} ${proteanBaits})"
        },
        projectionTetherBait: {
          en: "${mech1} (${spiteBaits} Baits) => ${mech2}",
          de: "${mech1} (${spiteBaits} K\xF6dern) => ${mech2}",
          cn: "${mech1} (${spiteBaits} Baits) => ${mech2}",
          ko: "${mech1} (${spiteBaits} \uC720\uB3C4) => ${mech2}"
        },
        manaBurstTetherHitbox: {
          en: "${mech1} + Avoid ${spiteBaits} Baits => ${mech2}",
          de: "${mech1} + Vermeide ${spiteBaits} K\xF6dern => ${mech2}",
          cn: "${mech1} + \u8EB2\u907F ${spiteBaits} \u8BF1\u5BFC => ${mech2}",
          ko: "${mech1} + ${spiteBaits} \uC720\uB3C4 \uD53C\uD558\uAE30 => ${mech2}"
        },
        heavySlamTetherBait: {
          en: "${mech1} (${spiteBaits} Baits) => ${mech2}",
          de: "${mech1} (${spiteBaits} K\xF6dern) => ${mech2}",
          cn: "${mech1} (${spiteBaits} \u8BF1\u5BFC) => ${mech2}",
          ko: "${mech1} (${spiteBaits} \uC720\uB3C4) => ${mech2}"
        },
        fireballSplashTetherHitbox: {
          en: "${mech1} + Avoid ${spiteBaits} Baits => ${mech2}",
          de: "${mech1} + Vermeide ${spiteBaits} K\xF6dern => ${mech2}",
          cn: "${mech1} + \u8EB2\u907F ${spiteBaits} \u8BF1\u5BFC => ${mech2}",
          ko: "${mech1} + ${spiteBaits} \uC720\uB3C4 \uD53C\uD558\uAE30 => ${mech2}"
        },
        noTetherHitbox: {
          en: "${mech1} + Avoid ${spiteBaits} Baits => ${mech2}",
          de: "${mech1} + Vermeide ${spiteBaits} K\xF6dern => ${mech2}",
          cn: "${mech1} + \u8EB2\u907F ${spiteBaits} \u8BF1\u5BFC => ${mech2}",
          ko: "${mech1} + ${spiteBaits} \uC720\uB3C4 \uD53C\uD558\uAE30 => ${mech2}"
        },
        stack: Voices.stackMarker,
        projection: {
          en: "Cones",
          de: "Klone",
          cn: "\u6247\u5F62",
          ko: "\uBD80\uCC44\uAF34"
        },
        defamation: {
          en: "Defamation",
          de: "Ehrenstrafe",
          cn: "\u5927\u5708",
          ko: "\uAD11\uC5ED\uC9D5"
        },
        unknown: Voices.unknown,
        netherwrathMechThenMech: {
          en: "${spiteBaits} Baits + ${mech1} N + ${mech2} S => ${mech3} NE + ${mech4} SW",
          de: "${spiteBaits} K\xF6dern + ${mech1} N + ${mech2} S => ${mech3} NO + ${mech4} SW",
          cn: "${spiteBaits} \u8BF1\u5BFC + ${mech1} \u4E0A + ${mech2} \u4E0B => ${mech3} \u53F3\u4E0A + ${mech4} \u5DE6\u4E0B",
          ko: "${spiteBaits} \uC720\uB3C4 + ${mech1} \uBD81 + ${mech2} \uB0A8 => ${mech3} \uBD81\uB3D9 + ${mech4} \uB0A8\uC11C"
        }
      }
    }),
    raws({
      id: "M12S Reenactment 1 Scalding Waves Collect (DN)",
      type: "Ability",
      netRegex: { id: "B8E1", source: "Lindwurm", capture: false },
      condition: (pull) => pull.phase === "reenactment1",
      suppressSeconds: 9999,
      run: (pull) => pull.netherwrathFollowup = true
    }),
    raws({
      id: "M12S Reenactment 1 Clone Stack SW (Second Clones Banana/Nukemaru)",
      type: "Ability",
      netRegex: { id: "B922", source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.replication2StrategyDetected === "banana" || pull.replication2StrategyDetected === "nukemaru")
          return true;
        return false;
      },
      suppressSeconds: 9999,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          stackThenStackBanana: {
            en: "Stack on SW Clone => Stack on NW Clone",
            de: "Auf SW Klon sammeln => Auf NW Klon sammeln",
            cn: "\u5DE6\u4E0B\u5206\u8EAB\u5206\u644A => \u5DE6\u4E0A\u5206\u8EAB\u5206\u644A",
            ko: "\uB0A8\uC11C \uBD84\uC2E0\uC5D0\uC11C \uC250\uC5B4 => \uBD81\uC11C \uBD84\uC2E0\uC5D0\uC11C \uC250\uC5B4"
          },
          avoidStackThenProteanBanana: {
            en: "Avoid SW Stack => Bait Protean West",
            de: "Vermeide SW sammeln => K\xF6der Kegel-AoE West",
            cn: "\u907F\u5F00\u5DE6\u4E0B\u5206\u644A => \u5DE6\u4FA7\u8BF1\u5BFC\u6247\u5F62",
            ko: "\uB0A8\uC11C \uBD84\uC2E0 \uC250\uC5B4 \uD53C\uD558\uAE30 => \uC11C\uCABD\uC5D0\uC11C \uBD80\uCC44\uAF34 \uC720\uB3C4"
          },
          stackThenProteansBanana: {
            en: "SW Clone Stack => West Proteans",
            de: "SW Klon sammeln => West Kegel-Aoe",
            cn: "\u5DE6\u4E0B\u5206\u8EAB\u5206\u644A => \u5DE6\u4FA7\u6247\u5F62",
            ko: "\uB0A8\uC11C \uBD84\uC2E0 \uC250\uC5B4 => \uC11C\uCABD \uBD80\uCC44\uAF34"
          },
          stackThenStackNukemaru: {
            en: "Stack on NE Clone => Stack on SE Clone",
            de: "Auf NO Klon sammeln => Auf SO Klon sammeln",
            cn: "\u53F3\u4E0A\u5206\u8EAB\u5206\u644A => \u53F3\u4E0B\u5206\u8EAB\u5206\u644A",
            ko: "\uBD81\uB3D9 \uBD84\uC2E0\uC5D0\uC11C \uC250\uC5B4 => \uB0A8\uB3D9 \uBD84\uC2E0\uC5D0\uC11C \uC250\uC5B4"
          },
          avoidStackThenProteanNukemaru: {
            en: "Avoid NE Stack => Bait Protean East",
            de: "Vermeide NO sammeln => K\xF6der Kegel-AoE Ost",
            cn: "\u907F\u5F00\u53F3\u4E0A\u5206\u644A => \u53F3\u4FA7\u8BF1\u5BFC\u6247\u5F62",
            ko: "\uBD81\uB3D9 \uBD84\uC2E0 \uC250\uC5B4 \uD53C\uD558\uAE30 => \uB3D9\uCABD\uC5D0\uC11C \uBD80\uCC44\uAF34 \uC720\uB3C4"
          },
          stackThenProteansNukemaru: {
            en: "NE Clone Stack => East Proteans",
            de: "NO Klon sammeln => Ost Kegel-Aoe",
            cn: "\u53F3\u4E0A\u5206\u8EAB\u5206\u644A => \u53F3\u4FA7\u6247\u5F62",
            ko: "\uBD81\uB3D9 \uBD84\uC2E0 \uC250\uC5B4 => \uB3D9\uCABD \uBD80\uCC44\uAF34"
          }
        };
        const strats = pull.replication2StrategyDetected;
        const skill = pull.replication2PlayerAbilities[pull.me];
        switch (skill) {
          case headSignState["projectionTether"]:
          case headSignState["heavySlamTether"]:
            return {
              infoText: strats === "banana" ? voice.avoidStackThenProteanBanana() : voice.avoidStackThenProteanNukemaru()
            };
          case headSignState["manaBurstTether"]:
          case headSignState["fireballSplashTether"]:
          case "none":
            return {
              alertText: strats === "banana" ? voice.stackThenStackBanana() : voice.stackThenStackNukemaru()
            };
        }
        return {
          infoText: strats === "banana" ? voice.stackThenProteansBanana() : voice.stackThenProteansNukemaru()
        };
      }
    }),
    raws({
      id: "M12S Reenactment 1 Clone Stacks E/W (Third Clones DN)",
      type: "Ability",
      netRegex: { id: "BBE3", source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.netherwrathFollowup) {
          const sequence = pull.replication2AbilityOrder;
          const stack = headSignState["heavySlamTether"];
          const slur = headSignState["manaBurstTether"];
          const projections = headSignState["projectionTether"];
          if (sequence[rep2FacingSpotTable["east"]] === stack && sequence[rep2FacingSpotTable["west"]] === stack && sequence[rep2FacingSpotTable["northeast"]] === projections && sequence[rep2FacingSpotTable["southwest"]] === slur)
            return true;
        }
        return false;
      },
      suppressSeconds: 9999,
      alertText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "East/West Clone Stacks",
          de: "Ost/West Klon sammeln",
          cn: "\u5DE6/\u53F3\u5206\u8EAB\u5206\u644A",
          ko: "\uB3D9/\uC11C \uBD84\uC2E0 \uC250\uC5B4"
        }
      }
    }),
    raws({
      id: "M12S Reenactment 1 Proteans West (Third Clones Banana/Nukemaru)",
      type: "Ability",
      netRegex: { id: "BE5D", source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.replication2StrategyDetected === "banana" || pull.replication2StrategyDetected === "nukemaru")
          return true;
        return false;
      },
      suppressSeconds: 9999,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          proteanBanana: {
            en: "Bait Protean West + Avoid Clone AoE",
            de: "Kegel AoE k\xF6dern West + Vermeide Klon AoE",
            cn: "\u5DE6\u4FA7\u8BF1\u5BFC\u6247\u5F62 + \u907F\u5F00\u5206\u8EABAoE",
            ko: "\uC11C\uCABD \uBD80\uCC44\uAF34 \uC720\uB3C4 + \uBD84\uC2E0 \uC7A5\uD310 \uD53C\uD558\uAE30"
          },
          avoidThenStackBanana: {
            en: "Avoid West Clone/East Defamation + Stack on NW Clone",
            de: "Vermeide West Klon/Ost Ehrenstrafe + Sammeln auf NW Klon",
            cn: "\u907F\u5F00\u5DE6\u5206\u8EAB/\u53F3\u5927\u5708 + \u5DE6\u4E0A\u5206\u8EAB\u5206\u644A",
            ko: "\uC11C\uCABD \uBD84\uC2E0/\uB3D9\uCABD \uAD11\uC5ED\uC9D5 \uD53C\uD558\uAE30 + \uBD81\uC11C \uBD84\uC2E0\uC5D0\uC11C \uC250\uC5B4"
          },
          proteansThenStackBanana: {
            en: "West Proteans => NW Clone Stack",
            de: "West Kegel AoEs => NW Klon sammeln",
            cn: "\u5DE6\u4FA7\u6247\u5F62 => \u5DE6\u4E0A\u5206\u8EAB\u5206\u644A",
            ko: "\uC11C\uCABD \uBD80\uCC44\uAF34 => \uBD81\uC11C \uBD84\uC2E0 \uC250\uC5B4"
          },
          proteanNukemaru: {
            en: "Bait Protean East + Avoid Clone AoE",
            de: "Kegel AoE k\xF6dern Ost + Vermeide Klon AoE",
            cn: "\u53F3\u4FA7\u8BF1\u5BFC\u6247\u5F62 + \u907F\u5F00\u5206\u8EABAoE",
            ko: "\uB3D9\uCABD \uBD80\uCC44\uAF34 \uC720\uB3C4 + \uBD84\uC2E0 \uC7A5\uD310 \uD53C\uD558\uAE30"
          },
          avoidThenStackNukemaru: {
            en: "Avoid East Clone/West Defamation + Stack on SE Clone",
            de: "Vermeide Ost Klon/West Ehrenstrafe + Sammeln auf SO Klon",
            cn: "\u907F\u5F00\u53F3\u5206\u8EAB/\u5DE6\u5927\u5708 + \u53F3\u4E0A\u5206\u8EAB\u5206\u644A",
            ko: "\uB3D9\uCABD \uBD84\uC2E0/\uC11C\uCABD \uAD11\uC5ED\uC9D5 \uD53C\uD558\uAE30 + \uB0A8\uB3D9 \uBD84\uC2E0\uC5D0\uC11C \uC250\uC5B4"
          },
          proteansThenStackNukemaru: {
            en: "East Proteans => SE Clone Stack",
            de: "Ost Kegel AoEs => SO Klon sammeln",
            cn: "\u53F3\u4FA7\u6247\u5F62 => \u53F3\u4E0A\u5206\u8EAB\u5206\u644A",
            ko: "\uB3D9\uCABD \uBD80\uCC44\uAF34 => \uB0A8\uB3D9 \uBD84\uC2E0 \uC250\uC5B4"
          }
        };
        const strats = pull.replication2StrategyDetected;
        const skill = pull.replication2PlayerAbilities[pull.me];
        switch (skill) {
          case headSignState["projectionTether"]:
          case headSignState["heavySlamTether"]:
            return {
              alertText: strats === "banana" ? voice.proteanBanana() : voice.proteanNukemaru()
            };
          case headSignState["manaBurstTether"]:
          case headSignState["fireballSplashTether"]:
          case "none":
            return {
              infoText: strats === "banana" ? voice.avoidThenStackBanana() : voice.avoidThenStackNukemaru()
            };
        }
        return {
          infoText: strats === "banana" ? voice.proteansThenStackBanana() : voice.proteansThenStackNukemaru()
        };
      }
    }),
    raws({
      id: "M12S Reenactment 1 Defamation SE Dodge Reminder (Fourth Clones DN)",
      type: "Ability",
      netRegex: { id: "BE5D", source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.netherwrathFollowup) {
          const sequence = pull.replication2AbilityOrder;
          const stack = headSignState["heavySlamTether"];
          const slur = headSignState["manaBurstTether"];
          const projections = headSignState["projectionTether"];
          if (sequence[rep2FacingSpotTable["east"]] === stack && sequence[rep2FacingSpotTable["west"]] === stack && sequence[rep2FacingSpotTable["southeast"]] === slur && sequence[rep2FacingSpotTable["northwest"]] === projections)
            return true;
        }
        return false;
      },
      suppressSeconds: 9999,
      alertText: (_pull, _hit, voice) => voice.north(),
      outputStrings: {
        north: Voices.north
      }
    }),
    raws({
      id: "M12S Reenactment 1 Clone Stack NW Reminder (Fourth Clones Banana/Nukemaru)",
      type: "Ability",
      netRegex: { id: "B8E1", source: "Lindwurm", capture: false },
      condition: (pull) => {
        if (pull.replication2StrategyDetected === "banana" || pull.replication2StrategyDetected === "nukemaru")
          return true;
        return false;
      },
      suppressSeconds: 9999,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          stackBanana: {
            en: "Stack on NW Clone",
            de: "Auf NW Klon sammeln",
            cn: "\u5DE6\u4E0A\u5206\u8EAB\u5206\u644A",
            ko: "\uBD81\uC11C \uBD84\uC2E0\uC5D0\uC11C \uC250\uC5B4"
          },
          avoidStackBanana: {
            en: "Avoid NE Stack",
            de: "Vermeide NO sammeln",
            cn: "\u907F\u5F00\u53F3\u4E0A\u5206\u644A",
            ko: "\uBD81\uB3D9 \uC250\uC5B4 \uD53C\uD558\uAE30"
          },
          stackAndDefamationBanana: {
            en: "NW Clone Stack + SE Defamation",
            de: "NW Klon sammeln + SO Ehrenstrafe",
            cn: "\u5DE6\u4E0A\u5206\u8EAB\u5206\u644A + \u53F3\u4E0B\u5927\u5708",
            ko: "\uBD81\uC11C \uBD84\uC2E0 \uC250\uC5B4 + \uB0A8\uB3D9 \uAD11\uC5ED\uC9D5"
          },
          stackNukemaru: {
            en: "Stack on SE Clone",
            de: "Auf SO Klon sammeln",
            cn: "\u53F3\u4E0B\u5206\u8EAB\u5206\u644A",
            ko: "\uB0A8\uB3D9 \uBD84\uC2E0\uC5D0\uC11C \uC250\uC5B4"
          },
          avoidStackNukemaru: {
            en: "Avoid SE Stack",
            de: "Vermeide SO sammeln",
            cn: "\u907F\u5F00\u53F3\u4E0B\u5206\u644A",
            ko: "\uB0A8\uB3D9 \uC250\uC5B4 \uD53C\uD558\uAE30"
          },
          stackAndDefamationNukemaru: {
            en: "SE Clone Stack + NW Defamation",
            de: "SO Klon sammeln + NW Ehrenstrafe",
            cn: "\u53F3\u4E0B\u5206\u8EAB\u5206\u644A + \u5DE6\u4E0A\u5927\u5708",
            ko: "\uB0A8\uB3D9 \uBD84\uC2E0 \uC250\uC5B4 + \uBD81\uC11C \uAD11\uC5ED\uC9D5"
          }
        };
        const strats = pull.replication2StrategyDetected;
        const skill = pull.replication2PlayerAbilities[pull.me];
        switch (skill) {
          case headSignState["projectionTether"]:
          case headSignState["heavySlamTether"]:
            return {
              infoText: strats === "banana" ? voice.avoidStackBanana() : voice.avoidStackNukemaru()
            };
          case headSignState["manaBurstTether"]:
          case headSignState["fireballSplashTether"]:
          case "none":
            return {
              alertText: strats === "banana" ? voice.stackBanana() : voice.stackNukemaru()
            };
        }
        return {
          infoText: strats === "banana" ? voice.stackAndDefamationBanana() : voice.stackAndDefamationNukemaru()
        };
      }
    }),
    raws({
      id: "M12S Mana Sphere Collect and Label",
      type: "AddedCombatant",
      netRegex: { name: "Mana Sphere", capture: true },
      run: (pull, hit) => {
        const id = hit.id;
        const npcBaseCode = parseInt(hit.npcBaseId);
        switch (npcBaseCode) {
          case 19205:
            pull.manaSpheres[id] = "blackHole";
            return;
          case 19206:
            pull.manaSpheres[id] = "water";
            return;
          case 19207:
            pull.manaSpheres[id] = "wind";
            return;
          case 19208:
            pull.manaSpheres[id] = "lightning";
            return;
          case 19209:
            pull.manaSpheres[id] = "fire";
            return;
        }
      }
    }),
    raws({
      id: "M12S Mutation  Alpha/ Beta Collect",
      type: "GainsEffect",
      netRegex: { effectId: ["12A1", "12A3"], capture: true },
      condition: Condition.targetIsYou(),
      run: (pull, hit) => {
        pull.myMutation = hit.effectId === "12A1" ? "alpha" : "beta";
      }
    }),
    raws({
      id: "M12S Mutation  Alpha/ Beta",
      type: "GainsEffect",
      netRegex: { effectId: ["12A1", "12A3"], capture: true },
      condition: Condition.targetIsYou(),
      infoText: (_pull, hit, voice) => {
        if (hit.effectId === "12A1")
          return voice.alpha();
        return voice.beta();
      },
      tts: (_pull, hit, voice) => {
        if (hit.effectId === "12A1")
          return voice.alphaTts();
        return voice.betaTts();
      },
      outputStrings: {
        alpha: {
          en: "Mutation  Alpha on YOU",
          de: "Mutation  Alpha auf DIR",
          cn: "\u53D8\u5F02\u7EC6\u80DE Alpha\u70B9\u540D",
          ko: "\uBCC0\uC774\uC138\uD3EC  Alpha \uB300\uC0C1\uC790"
        },
        beta: {
          en: "Mutation  Beta on YOU",
          de: "Mutation  Beta auf DIR",
          cn: "\u53D8\u5F02\u7EC6\u80DE Beta\u70B9\u540D",
          ko: "\uBCC0\uC774\uC138\uD3EC  Beta \uB300\uC0C1\uC790"
        },
        alphaTts: {
          en: "Mutation  Alpha on YOU",
          de: "Mutation  Alpha auf DIR",
          cn: "\u53D8\u5F02\u7EC6\u80DE Alpha\u70B9\u540D",
          ko: "\uBCC0\uC774\uC138\uD3EC \uC54C\uD30C \uB300\uC0C1\uC790"
        },
        betaTts: {
          en: "Mutation  Beta on YOU",
          de: "Mutation  Beta auf DIR",
          cn: "\u53D8\u5F02\u7EC6\u80DE Beta\u70B9\u540D",
          ko: "\uBCC0\uC774\uC138\uD3EC \uBCA0\uD0C0 \uB300\uC0C1\uC790"
        }
      }
    }),
    raws({
      id: "M12S Mana Sphere Position Collect",
      type: "AbilityExtra",
      netRegex: { id: "B4FD", capture: true },
      run: (pull, hit) => {
        const readDistance = (x2Bit, y2Bit) => {
          const blackHoleXBit = x2Bit < 100 ? 90 : 110;
          const dxBit = x2Bit - blackHoleXBit;
          const dyBit = y2Bit - 100;
          return Math.round(Math.sqrt(dxBit * dxBit + dyBit * dyBit));
        };
        const x = parseFloat(hit.x);
        const y = parseFloat(hit.y);
        const d = readDistance(x, y);
        const id = hit.sourceId;
        if (x < 100) {
          pull.westManaSpheres[id] = { x, y };
        }
        pull.eastManaSpheres[id] = { x, y };
        if (d < 7) {
          pull.closeManaSphereIds.push(id);
          if (pull.closeManaSphereIds.length === 2) {
            const popFlank = x < 100 ? "east" : "west";
            pull.manaSpherePopSide = popFlank;
            const sphereId1s = pull.closeManaSphereIds[0];
            const sphereId2s = id;
            if (sphereId1s === void 0)
              return;
            const sphereType1s = pull.manaSpheres[sphereId1s];
            const sphereType2s = pull.manaSpheres[sphereId2s];
            if (sphereType1s === void 0 || sphereType2s === void 0)
              return;
            const nonPopFlank = popFlank === "east" ? "west" : "east";
            const lead = [sphereType1s, sphereType2s];
            const dir2s = lead.includes("water") ? popFlank : nonPopFlank;
            pull.firstBlackHole = dir2s;
          }
        }
      }
    }),
    raws({
      id: "M12S Black Hole and Shapes",
      type: "Ability",
      netRegex: { id: "B4FD", source: "Mana Sphere", capture: false },
      delaySeconds: 0.2,
      durationSeconds: 8.3,
      suppressSeconds: 9999,
      infoText: (pull, _hit, voice) => {
        const popFlank = pull.manaSpherePopSide;
        const blackHol = pull.firstBlackHole;
        const sphereId1s = pull.closeManaSphereIds[0];
        const sphereId2s = pull.closeManaSphereIds[1];
        if (popFlank === void 0 || blackHol === void 0 || sphereId1s === void 0 || sphereId2s === void 0)
          return pull.myMutation === "alpha" ? voice.alpha() : voice.beta();
        const sphereType1s = pull.manaSpheres[sphereId1s];
        const sphereType2s = pull.manaSpheres[sphereId2s];
        if (sphereType1s === void 0 || sphereType2s === void 0)
          return pull.myMutation === "alpha" ? voice.alpha() : voice.beta();
        if (pull.myMutation === "alpha")
          return voice.alphaDir({
            dir1: voice[popFlank](),
            shape1: voice[sphereType1s](),
            shape2: voice[sphereType2s](),
            northSouth: voice.northSouth(),
            dir2: voice[blackHol]()
          });
        return voice.betaDir({
          dir1: voice[popFlank](),
          shape1: voice[sphereType1s](),
          shape2: voice[sphereType2s](),
          northSouth: voice.northSouth(),
          dir2: voice[blackHol]()
        });
      },
      outputStrings: {
        east: Voices.east,
        west: Voices.west,
        northSouth: {
          en: "N/S",
          de: "N/S",
          fr: "N/S",
          ja: "\u5357/\u5317",
          cn: "\u4E0A/\u4E0B",
          ko: "\uB0A8/\uBD81",
          tc: "\u4E0A/\u4E0B"
        },
        water: {
          en: "Orb",
          de: "Orb",
          cn: "\u94A2\u94C1\u788E\u7247",
          ko: "\uAD6C\uC2AC"
        },
        lightning: {
          en: "Lightning",
          de: "Blitz",
          cn: "\u4E0A\u4E0B\u6247\u5F62\u788E\u7247",
          ko: "\uBC88\uAC1C"
        },
        fire: {
          en: "Fire",
          de: "Feuer",
          cn: "\u5DE6\u53F3\u6247\u5F62\u788E\u7247",
          ko: "\uBD88"
        },
        wind: {
          en: "Donut",
          de: "Donut",
          cn: "\u6708\u73AF\u788E\u7247",
          ko: "\uB3C4\uB11B"
        },
        alpha: {
          en: "Avoid Shape AoEs, Wait by Black Hole",
          de: "Vermeide Form-Aoe, Warte beim Schwarzen Loch",
          cn: "\u907F\u5F00\u788E\u7247 AOE, \u7B49\u5F85\u9ED1\u6D1E",
          ko: "\uB3C4\uD615 \uC7A5\uD310 \uD53C\uD558\uAE30, \uBE14\uB799\uD640 \uC606\uC5D0\uC11C \uB300\uAE30"
        },
        beta: {
          en: "Shared Shape Soak => Get by Black Hole",
          de: "Form-AoE zusammen nehmen => Geh zum Schwarzen Loch",
          cn: "\u5206\u644A\u649E\u788E\u7247 => \u9760\u8FD1\u9ED1\u6D1E",
          ko: "\uB3C4\uD615 \uC250\uC5B4 \uCC98\uB9AC => \uBE14\uB799\uD640 \uC606\uC73C\uB85C \uC774\uB3D9"
        },
        alphaDir: {
          en: "Avoid ${dir1} ${shape1}/${shape2} => ${dir2} Black Hole + ${northSouth}",
          de: "Vermeide ${dir1} ${shape1}/${shape2} Form-AoEs => ${dir2} Schwarzes Loch + ${northSouth}",
          cn: "\u907F\u5F00 ${dir1} ${shape1}/${shape2} => ${dir2} \u9ED1\u6D1E + ${northSouth}",
          ko: "${dir1} ${shape1}/${shape2} \uB3C4\uD615 \uC7A5\uD310 \uD53C\uD558\uAE30 => ${dir2} \uBE14\uB799\uD640 + ${northSouth}"
        },
        betaDir: {
          en: "Share ${dir1} ${shape1}/${shape2} => ${dir2} Black Hole + ${northSouth}",
          de: "Teile ${dir1} ${shape1}/${shape2} => ${dir2} Schwarzes Loch + ${northSouth}",
          cn: "\u5206\u644A ${dir1} ${shape1}/${shape2} => ${dir2} \u9ED1\u6D1E + ${northSouth}",
          ko: "${dir1} ${shape1}/${shape2} \uC250\uC5B4 => ${dir2} \uBE14\uB799\uD640 + ${northSouth}"
        }
      }
    }),
    raws({
      id: "M12S Dramatic Lysis Black Hole 1",
      type: "Ability",
      netRegex: { id: "B507", source: "Lindwurm", capture: false },
      durationSeconds: 15,
      suppressSeconds: 9999,
      alertText: (pull, _hit, voice) => {
        const blackHol = pull.firstBlackHole;
        if (blackHol === void 0)
          return pull.myMutation === "alpha" ? voice.alpha() : voice.beta();
        return pull.myMutation === "alpha" ? voice.alphaDir({
          northSouth: voice.northSouth(),
          dir2: voice[blackHol]()
        }) : voice.betaDir({
          northSouth: voice.northSouth(),
          dir2: voice[blackHol]()
        });
      },
      outputStrings: {
        east: Voices.east,
        west: Voices.west,
        northSouth: {
          en: "N/S",
          de: "N/S",
          fr: "N/S",
          ja: "\u5357/\u5317",
          cn: "\u4E0A/\u4E0B",
          ko: "\uB0A8/\uBD81",
          tc: "\u4E0A/\u4E0B"
        },
        alpha: {
          en: "Get by Black Hole",
          de: "Geh zum Schwarzen Loch",
          cn: "\u9760\u8FD1\u9ED1\u6D1E",
          ko: "\uBE14\uB799\uD640 \uC606\uC73C\uB85C \uC774\uB3D9"
        },
        beta: {
          en: "Get by Black Hole",
          de: "Geh zum Schwarzen Loch",
          cn: "\u9760\u8FD1\u9ED1\u6D1E",
          ko: "\uBE14\uB799\uD640 \uC606\uC73C\uB85C \uC774\uB3D9"
        },
        alphaDir: {
          en: "${dir2} Black Hole + ${northSouth}",
          de: "${dir2} Schwarzes Loch + ${northSouth}",
          cn: "${dir2} \u9ED1\u6D1E + ${northSouth}",
          ko: "${dir2} \uBE14\uB799\uD640 + ${northSouth}"
        },
        betaDir: {
          en: "${dir2} Black Hole + ${northSouth}",
          de: "${dir2} Schwarzes Loch + ${northSouth}",
          cn: "${dir2} \u9ED1\u6D1E + ${northSouth}",
          ko: "${dir2} \uBE14\uB799\uD640 + ${northSouth}"
        }
      }
    }),
    raws({
      id: "M12S Blood Wakening Followup",
      type: "Ability",
      netRegex: { id: ["B501", "B502", "B503", "B504"], source: "Lindwurm", capture: false },
      suppressSeconds: 9999,
      alertText: (pull, _hit, voice) => {
        const blackHol = pull.firstBlackHole;
        if (blackHol === void 0)
          return voice.move();
        const afterTwo = blackHol === "east" ? "west" : "east";
        return voice.moveDir({
          northSouth: voice.northSouth(),
          dir: voice[afterTwo]()
        });
      },
      outputStrings: {
        east: Voices.east,
        west: Voices.west,
        northSouth: {
          en: "N/S",
          de: "N/S",
          fr: "N/S",
          ja: "\u5357/\u5317",
          cn: "\u4E0A/\u4E0B",
          ko: "\uB0A8/\uBD81",
          tc: "\u4E0A/\u4E0B"
        },
        move: {
          en: "Move to other Black Hole",
          de: "Geh zum anderen Schwarzen Loch",
          cn: "\u53BB\u53E6\u4E00\u4E2A\u9ED1\u6D1E",
          ko: "\uB2E4\uB978 \uBE14\uB799\uD640\uB85C \uC774\uB3D9"
        },
        moveDir: {
          en: "${dir} Black Hole + ${northSouth}",
          de: "${dir} Schwarzen Loch + ${northSouth}",
          cn: "${dir} \u9ED1\u6D1E + ${northSouth}",
          ko: "${dir} \uBE14\uB799\uD640 + ${northSouth}"
        }
      }
    }),
    raws({
      id: "M12S Netherworld Near/Far",
      type: "StartsUsing",
      netRegex: { id: ["B52B", "B52C"], source: "Lindwurm", capture: true },
      alertText: (pull, hit, voice) => {
        if (hit.id === "B52B")
          return pull.myMutation === "beta" ? voice.betaNear({ mech: voice.getUnder() }) : voice.alphaNear({ mech: voice.maxMelee() });
        return pull.myMutation === "beta" ? voice.betaFar({ mech: voice.maxMelee() }) : voice.alphaFar({ mech: voice.getUnder() });
      },
      tts: (pull, hit, voice) => {
        if (hit.id === "B52B")
          return pull.myMutation === "beta" ? voice.betaNearTts({ mech: voice.getUnder() }) : voice.alphaNear({ mech: voice.maxMelee() });
        return pull.myMutation === "beta" ? voice.betaFarTts({ mech: voice.maxMelee() }) : voice.alphaFar({ mech: voice.getUnder() });
      },
      outputStrings: {
        getUnder: Voices.getUnder,
        maxMelee: {
          en: "Max Melee",
          de: "Max Nahkampf",
          cn: "\u6700\u5927\u8FD1\u6218\u8DDD\u79BB",
          ko: "\uCE7C\uB05D\uB51C"
        },
        alphaNear: {
          en: "${mech} (Avoid Near Stack)",
          de: "${mech} (Vermeide Nah-Sammeln)",
          cn: "${mech} (\u907F\u5F00\u8FD1\u5206\u644A)",
          ko: "${mech} (\uADFC\uAC70\uB9AC \uC250\uC5B4 \uD53C\uD558\uAE30)"
        },
        alphaFar: {
          en: "${mech} (Avoid Far Stack)",
          de: "${mech} (Vermeide Fern-Sammeln)",
          cn: "${mech} (\u907F\u5F00\u8FDC\u5206\u644A)",
          ko: "${mech} (\uC6D0\uAC70\uB9AC \uC250\uC5B4 \uD53C\uD558\uAE30)"
        },
        betaNear: {
          en: "Near  Beta Stack: ${mech}",
          de: "Nah  Beta Sammeln: ${mech}",
          cn: "\u8FD1  Beta \u5206\u644A: ${mech}",
          ko: "\uADFC\uAC70\uB9AC  Beta \uC250\uC5B4: ${mech}"
        },
        betaFar: {
          en: "Far  Beta Stack: ${mech}",
          de: "Fern  Beta Sammeln: ${mech}",
          cn: "\u8FDC  Beta \u5206\u644A: ${mech}",
          ko: "\uC6D0\uAC70\uB9AC  Beta \uC250\uC5B4: ${mech}"
        },
        betaNearTts: {
          en: "Near  Beta Stack: ${mech}",
          de: "Nah  Beta Sammeln: ${mech}",
          cn: "\u8FD1  Beta \u5206\u644A: ${mech}",
          ko: "\uADFC\uAC70\uB9AC \uBCA0\uD0C0 \uC250\uC5B4: ${mech}"
        },
        betaFarTts: {
          en: "Far  Beta Stack: ${mech}",
          de: "Fern  Beta Sammeln: ${mech}",
          cn: "\u8FDC  Beta \u5206\u644A: ${mech}",
          ko: "\uC6D0\uAC70\uB9AC \uBCA0\uD0C0 \uC250\uC5B4: ${mech}"
        }
      }
    }),
    raws({
      id: "M12S Idyllic Dream",
      type: "StartsUsing",
      netRegex: { id: "B509", source: "Lindwurm", capture: false },
      durationSeconds: 4.7,
      response: Response.bigAoe("alert")
    }),
    raws({
      id: "M12S Idyllic Dream Staging 2 Clone Order Collect",
      type: "ActorControlExtra",
      netRegex: { category: "0197", param1: "11D2", capture: true },
      condition: (pull) => {
        if (pull.phase === "idyllic" && pull.replicationCounter === 2)
          return true;
        return false;
      },
      run: (pull, hit) => {
        const actor = pull.actorPositions[hit.id];
        if (actor === void 0)
          return;
        const facingDigit = Facings.xyTo8DirNum(actor.x, actor.y, centers.x, centers.y);
        pull.replication3CloneOrder.push(facingDigit);
      }
    }),
    raws({
      id: "M12S Idyllic Dream Staging 2 First Clone Cardinal/Intercardinal",
      type: "ActorControlExtra",
      netRegex: { category: "0197", param1: "11D2", capture: true },
      condition: (pull) => {
        if (pull.phase === "idyllic" && pull.replicationCounter === 2)
          return true;
        return false;
      },
      suppressSeconds: 9999,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.id];
        if (actor === void 0)
          return;
        const facingDigit = Facings.xyTo8DirNum(actor.x, actor.y, centers.x, centers.y);
        if (facingDigit % 2 === 0)
          return voice.firstClone({ cards: voice.cardinals() });
        return voice.firstClone({ cards: voice.intercards() });
      },
      outputStrings: {
        cardinals: Voices.cardinals,
        intercards: Voices.intercards,
        firstClone: {
          en: "First Clone: ${cards}",
          de: "Erste Klone: ${cards}",
          cn: "\u7B2C\u4E00\u4E2A\u5206\u8EAB: ${cards}",
          ko: "\uCCAB \uBC88\uC9F8 \uBD84\uC2E0: ${cards}"
        }
      }
    }),
    raws({
      id: "M12S Idyllic Dream Staging 2 Tethered Clone Collect",
      type: "Tether",
      netRegex: { id: headSignState["lockedTether"], capture: true },
      condition: (pull) => {
        if (pull.phase === "idyllic" && pull.replicationCounter === 2)
          return true;
        return false;
      },
      run: (pull, hit) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === void 0)
          return;
        const facingDigit = Facings.xyTo8DirNum(actor.x, actor.y, centers.x, centers.y);
        pull.replication3CloneDirNumPlayers[facingDigit] = hit.target;
      }
    }),
    raws({
      id: "M12S Idyllic Dream Staging 2 Tethered Clone",
      type: "Tether",
      netRegex: { id: headSignState["lockedTether"], capture: true },
      condition: (pull, hit) => {
        if (pull.phase === "idyllic" && pull.replicationCounter === 2 && pull.me === hit.target)
          return true;
        return false;
      },
      suppressSeconds: 9999,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === void 0)
          return voice.cloneTether();
        const facingDigit = Facings.xyTo8DirNum(actor.x, actor.y, centers.x, centers.y);
        const facingTwo = Facings.output8Dir[facingDigit] ?? "unknown";
        return voice.cloneTetherDir({ dir: voice[facingTwo]() });
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        cloneTether: {
          en: "Tethered to Clone",
          de: "Verbunden zum Klon",
          cn: "\u5206\u8EAB\u8FDE\u7EBF",
          ko: "\uBD84\uC2E0\uACFC \uC120 \uC5F0\uACB0"
        },
        cloneTetherDir: {
          en: "Tethered to ${dir} Clone",
          de: "Verbunden zum ${dir} Klon",
          cn: "\u88AB ${dir} \u5206\u8EAB\u8FDE\u7EBF",
          ko: "${dir} \uBD84\uC2E0\uACFC \uC120 \uC5F0\uACB0"
        }
      }
    }),
    raws({
      id: "M12S Idyllic Dream Power Gusher and Snaking Kick Collect",
      type: "StartsUsing",
      netRegex: { id: ["B50F", "B510", "B511"], source: "Lindschrat", capture: true },
      run: (pull, hit) => {
        switch (hit.id) {
          case "B510": {
            const y = parseFloat(hit.y);
            pull.idyllicVision2NorthSouthCleaveSpot = y < centers.y ? "north" : "south";
            pull.idyllicDreamActorNS = hit.sourceId;
            return;
          }
          case "B511":
            pull.idyllicDreamActorSnaking = hit.sourceId;
            return;
          case "B50F":
            pull.idyllicDreamActorEW = hit.sourceId;
            return;
        }
      }
    }),
    raws({
      id: "M12S Idyllic Dream Power Gusher Vision",
      type: "StartsUsing",
      netRegex: { id: "B510", source: "Lindschrat", capture: true },
      infoText: (_pull, hit, voice) => {
        const y = parseFloat(hit.y);
        const facingTwo = y < centers.y ? "north" : "south";
        return voice.text({ dir: voice[facingTwo](), sides: voice.sides() });
      },
      outputStrings: {
        north: Voices.north,
        south: Voices.south,
        sides: Voices.sides,
        text: {
          en: "${dir} + ${sides} (later)",
          de: "${dir} + ${sides} (sp\xE4ter)",
          cn: "${dir} + ${sides} (\u7A0D\u540E)",
          ko: "${dir} + ${sides} (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M12S Replication 4 Ability Tethers Initial Call",
      type: "Tether",
      netRegex: {
        id: [
          headSignState["manaBurstTether"],
          headSignState["heavySlamTether"]
        ],
        capture: true
      },
      condition: (pull, hit) => {
        if (pull.me === hit.target && pull.phase === "idyllic")
          return true;
        return false;
      },
      delaySeconds: 0.1,
      durationSeconds: 7,
      suppressSeconds: 9999,
      infoText: (pull, _hit, voice) => {
        const lead = pull.replication4DirNumAbility[0];
        if (lead === void 0) {
          return voice.getTether();
        }
        const bit = lead === headSignState["heavySlamTether"] ? "stacks" : lead === headSignState["manaBurstTether"] ? "defamations" : "unknown";
        const clone = pull.replication3CloneDirNumPlayers;
        const strats = pull.triggerSetConfig.replication4Strategy;
        const myFacingDigit = Object.keys(clone).find(
          (tagTwo) => clone[parseInt(tagTwo)] === pull.me
        );
        if (myFacingDigit !== void 0) {
          switch (parseInt(myFacingDigit)) {
            case 0:
              return voice.mechLaterNClone({
                later: voice.mechLater({ mech: voice[bit]() }),
                tether: strats === "dn" ? voice.getStackEastGroupQuad1DN({
                  dir: bit === "stacks" ? voice["dirN"]() : bit === "defamations" ? voice["dirNE"]() : voice["unknown"]()
                }) : strats === "em" ? bit === "stacks" ? voice.getStackWestGroup1EM({
                  dir: voice["dirN"]()
                }) : bit === "defamations" ? voice.getStackWestGroup2EM({
                  dir: voice["dirSW"]()
                }) : voice.getStackWestGroup12EM({
                  dir1: voice["dirN"](),
                  dir2: voice["dirSW"]()
                }) : strats === "caro" ? voice.getDefamationEastGroupQuad1Caro({
                  dir: bit === "defamations" ? voice["dirN"]() : bit === "stacks" ? voice["dirNE"]() : voice["unknown"]()
                }) : strats === "nukemaru" ? voice.getStackEastGroupQuad1Nukemaru({
                  dir: bit === "stacks" ? voice["dirN"]() : bit === "defamations" ? voice["dirNE"]() : voice["unknown"]()
                }) : voice.getTether()
              });
            case 1:
              return voice.mechLaterNEClone({
                later: voice.mechLater({ mech: voice[bit]() }),
                tether: strats === "dn" ? voice.getStackEastGroupQuad2DN({
                  dir: bit === "stacks" ? voice["dirE"]() : bit === "defamations" ? voice["dirSE"]() : voice["unknown"]()
                }) : strats === "em" ? bit === "stacks" ? voice.getStackEastGroup1EM({
                  dir: voice["dirS"]()
                }) : bit === "defamations" ? voice.getStackEastGroup2EM({
                  dir: voice["dirNE"]()
                }) : voice.getStackEastGroup12EM({
                  dir1: voice["dirS"](),
                  dir2: voice["dirNE"]()
                }) : strats === "caro" ? voice.getStackEastGroupQuad2Caro({
                  dir: bit === "stacks" ? voice["dirE"]() : bit === "defamations" ? voice["dirSE"]() : voice["unknown"]()
                }) : strats === "nukemaru" ? voice.getDefamationEastGroupQuad1Nukemaru({
                  dir: bit === "defamations" ? voice["dirN"]() : bit === "stacks" ? voice["dirNE"]() : voice["unknown"]()
                }) : voice.getTether()
              });
            case 2:
              return voice.mechLaterEClone({
                later: voice.mechLater({ mech: voice[bit]() }),
                tether: strats === "dn" ? voice.getStackWestGroupQuad3DN({
                  dir: bit === "stacks" ? voice["dirS"]() : bit === "defamations" ? voice["dirSW"]() : voice["unknown"]()
                }) : strats === "em" ? bit === "stacks" ? voice.getStackEastGroup3EM({
                  dir: voice["dirE"]()
                }) : bit === "defamations" ? voice.getStackEastGroup4EM({
                  dir: voice["dirSE"]()
                }) : voice.getStackEastGroup34EM({
                  dir1: voice["dirE"](),
                  dir2: voice["dirSE"]()
                }) : strats === "caro" ? voice.getStackEastGroupQuad3Caro({
                  dir: bit === "stacks" ? voice["dirS"]() : bit === "defamations" ? voice["dirSW"]() : voice["unknown"]()
                }) : strats === "nukemaru" ? voice.getDefamationWestGroupQuad4Nukemaru({
                  dir: bit === "defamations" ? voice["dirW"]() : bit === "stacks" ? voice["dirNW"]() : voice["unknown"]()
                }) : voice.getTether()
              });
            case 3:
              return voice.mechLaterSEClone({
                later: voice.mechLater({ mech: voice[bit]() }),
                tether: strats === "dn" ? voice.getStackWestGroupQuad4DN({
                  dir: bit === "stacks" ? voice["dirW"]() : bit === "defamations" ? voice["dirNW"]() : voice["unknown"]()
                }) : strats === "em" ? bit === "defamations" ? voice.getDefamationEastGroup3EM({
                  dir: voice["dirE"]()
                }) : bit === "stacks" ? voice.getDefamationEastGroup4EM({
                  dir: voice["dirSE"]()
                }) : voice.getDefamationEastGroup34EM({
                  dir1: voice["dirE"](),
                  dir2: voice["dirSE"]()
                }) : strats === "caro" ? voice.getDefamationEastGroupQuad4Caro({
                  dir: bit === "defamations" ? voice["dirW"]() : bit === "stacks" ? voice["dirNW"]() : voice["unknown"]()
                }) : strats === "nukemaru" ? voice.getDefamationEastGroupQuad2Nukemaru({
                  dir: bit === "defamations" ? voice["dirE"]() : bit === "stacks" ? voice["dirSE"]() : voice["unknown"]()
                }) : voice.getTether()
              });
            case 4:
              return voice.mechLaterSClone({
                later: voice.mechLater({ mech: voice[bit]() }),
                tether: strats === "dn" ? voice.getDefamationEastGroupQuad1DN({
                  dir: bit === "defamations" ? voice["dirN"]() : bit === "stacks" ? voice["dirNE"]() : voice["unknown"]()
                }) : strats === "em" ? bit === "defamations" ? voice.getDefamationEastGroup1EM({
                  dir: voice["dirS"]()
                }) : bit === "stacks" ? voice.getDefamationEastGroup2EM({
                  dir: voice["dirNE"]()
                }) : voice.getDefamationEastGroup12EM({
                  dir1: voice["dirS"](),
                  dir2: voice["dirNE"]()
                }) : strats === "caro" ? voice.getDefamationWestGroupQuad1Caro({
                  dir: bit === "defamations" ? voice["dirN"]() : bit === "stacks" ? voice["dirNE"]() : voice["unknown"]()
                }) : strats === "nukemaru" ? voice.getDefamationWestGroupQuad3Nukemaru({
                  dir: bit === "defamations" ? voice["dirS"]() : bit === "stacks" ? voice["dirSW"]() : voice["unknown"]()
                }) : voice.getTether()
              });
            case 5:
              return voice.mechLaterSWClone({
                later: voice.mechLater({ mech: voice[bit]() }),
                tether: strats === "dn" ? voice.getDefamationEastGroupQuad2DN({
                  dir: bit === "defamations" ? voice["dirE"]() : bit === "stacks" ? voice["dirSE"]() : voice["unknown"]()
                }) : strats === "em" ? bit === "defamations" ? voice.getDefamationWestGroup1EM({
                  dir: voice["dirN"]()
                }) : bit === "stacks" ? voice.getDefamationWestGroup2EM({
                  dir: voice["dirSE"]()
                }) : voice.getDefamationWestGroup12EM({
                  dir1: voice["dirN"](),
                  dir2: voice["dirSE"]()
                }) : strats === "caro" ? voice.getStackWestGroupQuad2Caro({
                  dir: bit === "stacks" ? voice["dirE"]() : bit === "defamations" ? voice["dirSE"]() : voice["unknown"]()
                }) : strats === "nukemaru" ? voice.getStackWestGroupQuad3Nukemaru({
                  dir: bit === "stacks" ? voice["dirS"]() : bit === "defamations" ? voice["dirSW"]() : voice["unknown"]()
                }) : voice.getTether()
              });
            case 6:
              return voice.mechLaterWClone({
                later: voice.mechLater({ mech: voice[bit]() }),
                tether: strats === "dn" ? voice.getDefamationWestGroupQuad3DN({
                  dir: bit === "defamations" ? voice["dirS"]() : bit === "stacks" ? voice["dirSW"]() : voice["unknown"]()
                }) : strats === "em" ? bit === "defamations" ? voice.getDefamationWestGroup3EM({
                  dir: voice["dirW"]()
                }) : bit === "stacks" ? voice.getDefamationWestGroup4EM({
                  dir: voice["dirNW"]()
                }) : voice.getDefamationWestGroup34EM({
                  dir1: voice["dirW"](),
                  dir2: voice["dirNW"]()
                }) : strats === "caro" ? voice.getStackWestGroupQuad3Caro({
                  dir: bit === "stacks" ? voice["dirS"]() : bit === "defamations" ? voice["dirSW"]() : voice["unknown"]()
                }) : strats === "nukemaru" ? voice.getStackWestGroupQuad4Nukemaru({
                  dir: bit === "stacks" ? voice["dirW"]() : bit === "defamations" ? voice["dirNW"]() : voice["unknown"]()
                }) : voice.getTether()
              });
            case 7:
              return voice.mechLaterNWClone({
                later: voice.mechLater({ mech: voice[bit]() }),
                tether: strats === "dn" ? voice.getDefamationWestGroupQuad4DN({
                  dir: bit === "defamations" ? voice["dirW"]() : bit === "stacks" ? voice["dirNW"]() : voice["unknown"]()
                }) : strats === "em" ? bit === "stacks" ? voice.getStackWestGroup3EM({
                  dir: voice["dirW"]()
                }) : bit === "defamations" ? voice.getStackWestGroup4EM({
                  dir: voice["dirNW"]()
                }) : voice.getStackWestGroup34EM({
                  dir1: voice["dirW"](),
                  dir2: voice["dirNW"]()
                }) : strats === "caro" ? voice.getDefamationWestGroupQuad4Caro({
                  dir: bit === "defamations" ? voice["dirW"]() : bit === "stacks" ? voice["dirNW"]() : voice["unknown"]()
                }) : strats === "nukemaru" ? voice.getStackEastGroupQuad2Nukemaru({
                  dir: bit === "stacks" ? voice["dirE"]() : bit === "defamations" ? voice["dirSE"]() : voice["unknown"]()
                }) : voice.getTether()
              });
          }
        }
        return voice.mechLaterTether({
          later: voice.mechLater({ mech: voice[bit]() }),
          tether: voice.getTether()
        });
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        getTether: {
          en: "Get Tether",
          de: "Nimm Verbindung",
          cn: "\u63A5\u7EBF",
          ko: "\uC120 \uAC00\uC838\uAC00\uAE30"
        },
        mechLater: {
          en: "${mech} First (later)",
          de: "${mech} Zuerst (sp\xE4ter)",
          cn: "${mech} \u5148 (\u7A0D\u540E)",
          ko: "${mech} \uBA3C\uC800 (\uB098\uC911\uC5D0)"
        },
        defamations: {
          en: "Defamations",
          de: "Gro\xDFe AoE auf dir",
          fr: "Grosse AoE sur vous",
          ja: "\u81EA\u5206\u306B\u5DE8\u5927\u306A\u7206\u767A",
          cn: "\u5927\u5708",
          ko: "\uAD11\uC5ED\uC9D5",
          tc: "\u5927\u5708\u9EDE\u540D"
        },
        stacks: Voices.stacks,
        mechLaterTether: {
          en: "${later}; ${tether}",
          de: "${later}; ${tether}",
          cn: "${later}; ${tether}",
          ko: "${later}; ${tether}"
        },
        mechLaterNClone: {
          en: "${later}; ${tether}",
          de: "${later}; ${tether}",
          cn: "${later}; ${tether}",
          ko: "${later}; ${tether}"
        },
        mechLaterNEClone: {
          en: "${later}; ${tether}",
          de: "${later}; ${tether}",
          cn: "${later}; ${tether}",
          ko: "${later}; ${tether}"
        },
        mechLaterEClone: {
          en: "${later}; ${tether}",
          de: "${later}; ${tether}",
          cn: "${later}; ${tether}",
          ko: "${later}; ${tether}"
        },
        mechLaterSEClone: {
          en: "${later}; ${tether}",
          de: "${later}; ${tether}",
          cn: "${later}; ${tether}",
          ko: "${later}; ${tether}"
        },
        mechLaterSClone: {
          en: "${later}; ${tether}",
          de: "${later}; ${tether}",
          cn: "${later}; ${tether}",
          ko: "${later}; ${tether}"
        },
        mechLaterSWClone: {
          en: "${later}; ${tether}",
          de: "${later}; ${tether}",
          cn: "${later}; ${tether}",
          ko: "${later}; ${tether}"
        },
        mechLaterWClone: {
          en: "${later}; ${tether}",
          de: "${later}; ${tether}",
          cn: "${later}; ${tether}",
          ko: "${later}; ${tether}"
        },
        mechLaterNWClone: {
          en: "${later}; ${tether}",
          de: "${later}; ${tether}",
          cn: "${later}; ${tether}",
          ko: "${later}; ${tether}"
        },
        getStackEastGroupQuad1DN: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroupQuad2DN: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroupQuad3DN: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroupQuad4DN: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroupQuad1DN: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroupQuad2DN: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroupQuad3DN: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroupQuad4DN: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroup1EM: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroup2EM: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroup12EM: {
          en: "Get ${dir1}/${dir2} Stack Tether",
          de: "Nimm ${dir1}/${dir2} Sammel-Verbindung",
          cn: "\u63A5${dir1}/${dir2}\u5206\u644A\u7EBF",
          ko: "${dir1}/${dir2} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroup1EM: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroup2EM: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroup12EM: {
          en: "Get ${dir1}/${dir2} Stack Tether",
          de: "Nimm ${dir1}/${dir2} Sammel-Verbindung",
          cn: "\u63A5${dir1}/${dir2}\u5206\u644A\u7EBF",
          ko: "${dir1}/${dir2} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroup3EM: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroup4EM: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroup34EM: {
          en: "Get ${dir1}/${dir2} Stack Tether",
          de: "Nimm ${dir1}/${dir2} Sammel-Verbindung",
          cn: "\u63A5${dir1}/${dir2}\u5206\u644A\u7EBF",
          ko: "${dir1}/${dir2} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroup3EM: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroup4EM: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroup34EM: {
          en: "Get ${dir1}/${dir2} Defamation Tether",
          de: "Nimm ${dir1}/${dir2} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir1}/${dir2}\u5927\u5708\u7EBF",
          ko: "${dir1}/${dir2} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroup1EM: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroup2EM: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroup12EM: {
          en: "Get ${dir1}/${dir2} Defamation Tether",
          de: "Nimm ${dir1}/${dir2} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir1}/${dir2}\u5927\u5708\u7EBF",
          ko: "${dir1}/${dir2} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroup1EM: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroup2EM: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroup12EM: {
          en: "Get ${dir1}/${dir2} Defamation Tether",
          de: "Nimm ${dir1}/${dir2} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir1}/${dir2}\u5927\u5708\u7EBF",
          ko: "${dir1}/${dir2} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroup3EM: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroup4EM: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroup34EM: {
          en: "Get ${dir1}/${dir2} Defamation Tether",
          de: "Nimm ${dir1}/${dir2} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir1}/${dir2}\u5927\u5708\u7EBF",
          ko: "${dir1}/${dir2} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroup3EM: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroup4EM: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroup34EM: {
          en: "Get ${dir1}/${dir2} Stack Tether",
          de: "Nimm ${dir1}/${dir2} Sammel-Verbindung",
          cn: "\u63A5${dir1}/${dir2}\u5206\u644A\u7EBF",
          ko: "${dir1}/${dir2} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroupQuad1Caro: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroupQuad2Caro: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroupQuad3Caro: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroupQuad4Caro: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroupQuad1Caro: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroupQuad2Caro: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroupQuad3Caro: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroupQuad4Caro: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroupQuad1Nukemaru: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroupQuad1Nukemaru: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroupQuad4Nukemaru: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationEastGroupQuad2Nukemaru: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getDefamationWestGroupQuad3Nukemaru: {
          en: "Get ${dir} Defamation Tether",
          de: "Nimm ${dir} Ehrenstrafe-Verbindung",
          cn: "\u63A5${dir}\u5927\u5708\u7EBF",
          ko: "${dir} \uAD11\uC5ED\uC9D5 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroupQuad3Nukemaru: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackWestGroupQuad4Nukemaru: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        },
        getStackEastGroupQuad2Nukemaru: {
          en: "Get ${dir} Stack Tether",
          de: "Nimm ${dir} Sammel-Verbindung",
          cn: "\u63A5${dir}\u5206\u644A\u7EBF",
          ko: "${dir} \uC250\uC5B4 \uC120 \uAC00\uC838\uAC00\uAE30"
        }
      }
    }),
    raws({
      id: "M12S Replication 4 Locked Tether Collect",
      type: "Tether",
      netRegex: { id: headSignState["lockedTether"], capture: true },
      condition: (pull) => {
        if (pull.phase === "idyllic" && pull.replicationCounter === 4)
          return true;
        return false;
      },
      run: (pull, hit) => {
        const actor = pull.actorPositions[hit.sourceId];
        const markThree = hit.target;
        if (actor === void 0) {
          if (pull.me === markThree)
            pull.replication4PlayerAbilities[markThree] = "unknown";
          return;
        }
        const facingDigit = Facings.xyTo8DirNum(
          actor.x,
          actor.y,
          centers.x,
          centers.y
        );
        pull.replication4BossCloneDirNumPlayers[facingDigit] = markThree;
        const skill = pull.replication4DirNumAbility[facingDigit];
        if (skill === void 0) {
          pull.replication4PlayerAbilities[markThree] = "unknown";
          return;
        }
        pull.replication4PlayerAbilities[markThree] = skill;
        if (Object.keys(pull.replication4PlayerAbilities).length === 8) {
          const abilitie = pull.replication4PlayerAbilities;
          const sequence = pull.replication3CloneOrder;
          const members = pull.replication3CloneDirNumPlayers;
          const lead = sequence[0];
          if (lead === void 0)
            return;
          const facingDigitSequence = lead % 2 === 0 ? [0, 2, 4, 6, 1, 3, 5, 7] : [1, 3, 5, 7, 0, 2, 4, 6];
          for (const facingNum2 of facingDigitSequence) {
            const player = members[facingNum2] ?? "unknown";
            const ability2s = abilitie[player] ?? "unknown";
            pull.replication4PlayerOrder.push(player);
            pull.replication4AbilityOrder.push(ability2s);
          }
        }
      }
    }),
    raws({
      id: "M12S Replication 4 Locked Tether",
      type: "Tether",
      netRegex: { id: headSignState["lockedTether"], capture: true },
      condition: (pull, hit) => {
        if (pull.phase === "idyllic" && pull.twistedVisionCounter === 3 && pull.me === hit.target)
          return true;
        return false;
      },
      delaySeconds: 0.1,
      durationSeconds: 8,
      alertText: (pull, hit, voice) => {
        const meteorAoe = voice.meteorAoe({
          bigAoe: voice.bigAoe(),
          groups: voice.healerGroups()
        });
        const sweepOrigin = pull.idyllicVision2NorthSouthCleaveSpot;
        const mySkill = pull.replication4PlayerAbilities[pull.me];
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === void 0 || sweepOrigin === void 0) {
          switch (mySkill) {
            case headSignState["manaBurstTether"]:
              return voice.manaBurstTether({ meteorAoe });
            case headSignState["heavySlamTether"]:
              return voice.heavySlamTether({ meteorAoe });
          }
          return;
        }
        const facingDigit = Facings.xyTo8DirNum(actor.x, actor.y, centers.x, centers.y);
        const facingTwo = Facings.output8Dir[facingDigit] ?? "unknown";
        const dodg = voice.dodgeCleaves({
          dir: voice[sweepOrigin](),
          sides: voice.sides()
        });
        switch (mySkill) {
          case headSignState["manaBurstTether"]:
            return voice.manaBurstTetherDir({
              dir: voice[facingTwo](),
              dodgeCleaves: dodg,
              meteorAoe
            });
          case headSignState["heavySlamTether"]:
            return voice.heavySlamTetherDir({
              dir: voice[facingTwo](),
              dodgeCleaves: dodg,
              meteorAoe
            });
        }
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        north: Voices.north,
        south: Voices.south,
        sides: Voices.sides,
        bigAoe: Voices.bigAoe,
        healerGroups: Voices.healerGroups,
        meteorAoe: {
          en: "${bigAoe} + ${groups}",
          de: "${bigAoe} + ${groups}",
          cn: "${bigAoe} + ${groups}",
          ko: "${bigAoe} + ${groups}"
        },
        dodgeCleaves: {
          en: "${dir} + ${sides}",
          de: "${dir} + ${sides}",
          cn: "${dir} + ${sides}",
          ko: "${dir} + ${sides}"
        },
        manaBurstTetherDir: {
          en: "${dodgeCleaves} (${dir} Defamation Tether) => ${meteorAoe}",
          de: "${dodgeCleaves} (${dir} Ehrenstrafe-Verbindung) => ${meteorAoe}",
          cn: "${dodgeCleaves} (${dir}\u5927\u5708\u7EBF) => ${meteorAoe}",
          ko: "${dodgeCleaves} (${dir} \uAD11\uC5ED\uC9D5 \uC120) => ${meteorAoe}"
        },
        manaBurstTether: {
          en: " N/S Clone (Defamation Tether) => ${meteorAoe}",
          de: " N/S Klon (Ehrenstrafe-Verbindung) => ${meteorAoe}",
          cn: " \u5357/\u5317\u5206\u8EAB (\u5927\u5708\u7EBF) => ${meteorAoe}",
          ko: " \uBD81/\uB0A8 \uBD84\uC2E0 (\uAD11\uC5ED\uC9D5 \uC120) => ${meteorAoe}"
        },
        heavySlamTetherDir: {
          en: "${dodgeCleaves} (${dir} Stack Tether) => ${meteorAoe}",
          de: "${dodgeCleaves} (${dir} Sammel-Verbindung) => ${meteorAoe}",
          cn: "${dodgeCleaves} (${dir}\u5206\u644A\u7EBF) => ${meteorAoe}",
          ko: "${dodgeCleaves} (${dir} \uC250\uC5B4\uC9D5 \uC120) => ${meteorAoe}"
        },
        heavySlamTether: {
          en: " N/S Clone (Stack Tether) => ${meteorAoe}",
          de: " N/S Klon (Sammel-Verbindung) => ${meteorAoe}",
          cn: " \u5357/\u5317\u5206\u8EAB (\u5206\u644A\u7EBF) => ${meteorAoe}",
          ko: " \uBD81/\uB0A8 \uBD84\uC2E0 (\uC250\uC5B4\uC9D5 \uC120) => ${meteorAoe}"
        }
      }
    }),
    raws({
      id: "M12S CombatantMemory Tower Collect",
      type: "CombatantMemory",
      netRegex: {
        change: "Add",
        pair: [{ key: "BNpcID", value: ["1EBF25", "1EBF26", "1EBF27", "1EBF28"] }],
        capture: true
      },
      suppressSeconds: 9999,
      run: (pull, hit) => {
        const x = parseFloat(hit.pairPosX ?? "0");
        const pillarTable = {
          "1EBF25": "wind",
          "1EBF26": "dark",
          "1EBF27": "earth",
          "1EBF28": "fire",
          "unknown": "unknown"
        };
        const pattern1s = ["earth", "wind", "dark", "fire"];
        const pattern2s = ["wind", "earth", "fire", "dark"];
        const bnpcids = hit.pairBNpcID ?? "unknown";
        const kinds = pillarTable[bnpcids];
        if (kinds === "earth" || kinds === "dark") {
          if (x > 81 && x < 83 || x > 109 && x < 111)
            pull.cosmicKissPattern = pattern1s;
          else
            pull.cosmicKissPattern = pattern2s;
        } else if (kinds === "wind" || kinds === "fire") {
          if (x > 81 && x < 83 || x > 109 && x < 111)
            pull.cosmicKissPattern = pattern2s;
          else
            pull.cosmicKissPattern = pattern1s;
        }
      }
    }),
    whenChant("B529").spread(),
    raws({
      id: "M12S Light Resistance Down II Collect",
      type: "GainsEffect",
      netRegex: { effectId: "1044", capture: true },
      condition: Condition.targetIsYou(),
      run: (pull) => pull.hasLightResistanceDown = true
    }),
    raws({
      id: "M12S Light Resistance Down II",
      type: "GainsEffect",
      netRegex: { effectId: "1044", capture: true },
      condition: (pull, hit) => {
        if (pull.twistedVisionCounter === 3 && pull.me === hit.target)
          return true;
        return false;
      },
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Soak Fire/Earth Meteor (later)",
          de: "Nimm Feuer/Erde Meteor (sp\xE4ter)",
          cn: "\u8E29\u706B/\u571F\u9668\u77F3\u5854 (\u7A0D\u540E)",
          ko: "\uBD88/\uB545 \uBA54\uD14C\uC624 \uBC1F\uAE30 (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M12S No Light Resistance Down II",
      type: "GainsEffect",
      netRegex: { effectId: "1044", capture: false },
      condition: (pull) => pull.twistedVisionCounter === 3,
      delaySeconds: 0.1,
      suppressSeconds: 9999,
      infoText: (pull, _hit, voice) => {
        if (!pull.hasLightResistanceDown)
          return voice.text();
      },
      outputStrings: {
        text: {
          en: "Soak a White/Star Meteor (later)",
          de: "Nimm Wei\xDFen/Stern Meteor (sp\xE4ter)",
          cn: "\u8E29\u5149/\u5F69\u8272\u9668\u77F3\u5854 (\u7A0D\u540E)",
          ko: "\uBC14\uB78C/\uC5B4\uB460 \uBA54\uD14C\uC624 \uBC1F\uAE30 (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M12S Twisted Vision 4 Stack/Defamation 1",
      type: "StartsUsing",
      netRegex: { id: "BBE2", source: "Lindwurm", capture: false },
      condition: (pull) => pull.twistedVisionCounter === 4,
      durationSeconds: 10,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          stacks: Voices.stacks,
          stackOnYou: Voices.stackOnYou,
          defamations: {
            en: "Avoid Defamations",
            de: "Vermeide Ehrenstrafe",
            cn: "\u8FDC\u79BB\u5927\u5708",
            ko: "\uAD11\uC5ED\uC9D5 \uD53C\uD558\uAE30"
          },
          defamationOnYou: Voices.defamationOnYou,
          stacksThenDefamations: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          defamationsThenStacks: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          stacksThenDefamationOnYou: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          defamationsThenStackOnYou: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          stackOnYouThenDefamations: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          defamationOnYouThenStack: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          }
        };
        const player1s = pull.replication4BossCloneDirNumPlayers[0];
        const player2s = pull.replication4BossCloneDirNumPlayers[4];
        const player3s = pull.replication4BossCloneDirNumPlayers[1];
        const player4s = pull.replication4BossCloneDirNumPlayers[5];
        const skillCode = pull.replication4DirNumAbility[0];
        if (skillCode === void 0 || player1s === void 0 || player2s === void 0 || player3s === void 0 || player4s === void 0)
          return;
        const ability1s = skillCode === headSignState["manaBurstTether"] ? "defamations" : skillCode === headSignState["heavySlamTether"] ? "stacks" : "unknown";
        if (ability1s === "stacks") {
          if (pull.me === player1s || pull.me === player2s)
            return {
              alertText: voice.stackOnYouThenDefamations({
                mech1: voice.stackOnYou(),
                mech2: voice.defamations()
              })
            };
          if (pull.me === player3s || pull.me === player4s)
            return {
              infoText: voice.stacksThenDefamationOnYou({
                mech1: voice.stacks(),
                mech2: voice.defamationOnYou()
              })
            };
          return {
            infoText: voice.stacksThenDefamations({
              mech1: voice.stacks(),
              mech2: voice.defamations()
            })
          };
        }
        if (ability1s === "defamations") {
          if (pull.me === player1s || pull.me === player2s)
            return {
              alertText: voice.defamationOnYouThenStack({
                mech1: voice.defamationOnYou(),
                mech2: voice.stacks()
              })
            };
          if (pull.me === player3s || pull.me === player4s)
            return {
              infoText: voice.defamationsThenStackOnYou({
                mech1: voice.defamations(),
                mech2: voice.stackOnYou()
              })
            };
          return {
            infoText: voice.defamationsThenStacks({
              mech1: voice.defamations(),
              mech2: voice.stacks()
            })
          };
        }
      }
    }),
    raws({
      id: "M12S Twisted Vision 4 Stack/Defamation Counter",
      type: "Ability",
      netRegex: { id: ["B519", "B517"], source: "Lindschrat", capture: false },
      condition: (pull) => pull.twistedVisionCounter === 4,
      suppressSeconds: 1,
      run: (pull) => {
        pull.twistedVision4MechCounter = pull.twistedVision4MechCounter + 2;
      }
    }),
    raws({
      id: "M12S Twisted Vision 4 Stack/Defamation 2-4",
      type: "Ability",
      netRegex: { id: ["B519", "B517"], source: "Lindschrat", capture: false },
      condition: (pull) => pull.twistedVisionCounter === 4 && pull.twistedVision4MechCounter <= 6,
      suppressSeconds: 1,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          stacks: Voices.stacks,
          stackOnYou: Voices.stackOnYou,
          defamations: {
            en: "Avoid Defamations",
            de: "Vermeide Ehrenstrafe",
            cn: "\u8FDC\u79BB\u5927\u5708",
            ko: "\uAD11\uC5ED\uC9D5 \uD53C\uD558\uAE30"
          },
          defamationOnYou: Voices.defamationOnYou,
          stacksThenDefamations: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          defamationsThenStacks: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          stacksThenDefamationOnYou: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          defamationsThenStackOnYou: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          stackOnYouThenDefamations: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          defamationOnYouThenStack: {
            en: "${mech1} => ${mech2}",
            de: "${mech1} => ${mech2}",
            cn: "${mech1} => ${mech2}",
            ko: "${mech1} => ${mech2}"
          },
          towers: {
            en: "Tower Positions",
            de: "Turm Positionen",
            fr: "Position tour",
            ja: "\u5854\u306E\u4F4D\u7F6E\u3078",
            cn: "\u5854\u7AD9\u4F4D",
            ko: "\uD0D1 \uC790\uB9AC\uC7A1\uAE30",
            tc: "\u516B\u4EBA\u5854\u7AD9\u4F4D"
          }
        };
        const tally = pull.twistedVision4MechCounter;
        const members = pull.replication4BossCloneDirNumPlayers;
        const skillCodes = pull.replication4DirNumAbility;
        const player1s = tally === 2 ? members[1] : tally === 4 ? members[2] : members[3];
        const player2s = tally === 2 ? members[5] : tally === 4 ? members[6] : members[7];
        const skillCode = tally === 2 ? skillCodes[1] : tally === 4 ? skillCodes[2] : skillCodes[3];
        if (skillCode === void 0 || player1s === void 0 || player2s === void 0)
          return;
        const ability1s = skillCode === headSignState["manaBurstTether"] ? "defamations" : skillCode === headSignState["heavySlamTether"] ? "stacks" : "unknown";
        if (tally < 6) {
          const player3s = tally === 2 ? members[2] : members[3];
          const player4s = tally === 2 ? members[6] : members[7];
          if (player3s === void 0 || player4s === void 0)
            return;
          if (ability1s === "stacks") {
            if (pull.me === player1s || pull.me === player2s)
              return {
                alertText: voice.stackOnYouThenDefamations({
                  mech1: voice.stackOnYou(),
                  mech2: voice.defamations()
                })
              };
            if (pull.me === player3s || pull.me === player4s)
              return {
                infoText: voice.stacksThenDefamationOnYou({
                  mech1: voice.stacks(),
                  mech2: voice.defamationOnYou()
                })
              };
            return {
              infoText: voice.stacksThenDefamations({
                mech1: voice.stacks(),
                mech2: voice.defamations()
              })
            };
          }
          if (ability1s === "defamations") {
            if (pull.me === player1s || pull.me === player2s)
              return {
                alertText: voice.defamationOnYouThenStack({
                  mech1: voice.defamationOnYou(),
                  mech2: voice.stacks()
                })
              };
            if (pull.me === player3s || pull.me === player4s)
              return {
                infoText: voice.defamationsThenStackOnYou({
                  mech1: voice.defamations(),
                  mech2: voice.stackOnYou()
                })
              };
            return {
              infoText: voice.defamationsThenStacks({
                mech1: voice.defamations(),
                mech2: voice.stacks()
              })
            };
          }
        }
        if (ability1s === "stacks") {
          if (pull.me === player1s || pull.me === player2s)
            return {
              alertText: voice.stackOnYouThenDefamations({
                mech1: voice.stackOnYou(),
                mech2: voice.towers()
              })
            };
          return {
            infoText: voice.stacksThenDefamations({
              mech1: voice.stacks(),
              mech2: voice.towers()
            })
          };
        }
        if (ability1s === "defamations") {
          if (pull.me === player1s || pull.me === player2s)
            return {
              alertText: voice.defamationOnYouThenStack({
                mech1: voice.defamationOnYou(),
                mech2: voice.towers()
              })
            };
          return {
            infoText: voice.defamationsThenStacks({
              mech1: voice.defamations(),
              mech2: voice.towers()
            })
          };
        }
      }
    }),
    raws({
      id: "M12S Twisted Vision 5 Towers (Early)",
      type: "Ability",
      netRegex: { id: ["B519", "B517"], source: "Lindschrat", capture: false },
      condition: (pull) => pull.twistedVisionCounter === 4 && pull.twistedVision4MechCounter > 6,
      durationSeconds: 5,
      suppressSeconds: 9999,
      infoText: (_pull, _hit, voice) => voice.towers(),
      outputStrings: {
        towers: {
          en: "Tower Positions",
          de: "Turm Positionen",
          fr: "Position tour",
          ja: "\u5854\u306E\u4F4D\u7F6E\u3078",
          cn: "\u5854\u7AD9\u4F4D",
          ko: "\uD0D1 \uC790\uB9AC\uC7A1\uAE30",
          tc: "\u516B\u4EBA\u5854\u7AD9\u4F4D"
        }
      }
    }),
    raws({
      id: "M12S Twisted Vision 5 Towers",
      type: "StartsUsing",
      netRegex: { id: "BBE2", source: "Lindwurm", capture: true },
      condition: (pull) => pull.twistedVisionCounter === 5,
      durationSeconds: (_pull, hit) => parseFloat(hit.castTime) + 4.1,
      alertText: (pull, _hit, voice) => {
        if (pull.hasLightResistanceDown)
          return voice.fireEarthTower();
        return voice.holyTower();
      },
      outputStrings: {
        fireEarthTower: {
          en: "Soak Fire/Earth Meteor",
          de: "Nimm Feuer/Erd Meteor",
          cn: "\u8E29\u706B/\u571F\u9668\u77F3\u5854",
          ko: "\uBD88/\uB545 \uBA54\uD14C\uC624 \uBC1F\uAE30"
        },
        holyTower: {
          en: "Soak a White/Star Meteor",
          de: "Nimm Wei\xDFen/Stern Meteor",
          cn: "\u8E29\u5149/\u5F69\u8272\u9668\u77F3\u5854",
          ko: "\uBC14\uB78C/\uC5B4\uB460 \uBA54\uD14C\uC624 \uBC1F\uAE30"
        }
      }
    }),
    raws({
      id: "M12S Cosmic Kiss Tower Collect",
      type: "Ability",
      netRegex: { id: "B4F4", source: "Lindwurm", capture: true },
      condition: Condition.targetIsYou(),
      delaySeconds: 0.1,
      run: (pull, hit) => {
        const actor = pull.actorPositions[hit.sourceId];
        const pillars = pull.cosmicKissPattern;
        if (actor === void 0 || pillars.length !== 4)
          return;
        const x = actor.x;
        const y = actor.y;
        if (x > 81 && x < 83 || x > 109 && x < 111) {
          pull.myCosmicKiss = pull.cosmicKissPattern[y < centers.y ? 0 : 2];
        } else if (x > 89 && x < 91 || x > 117 && x < 119) {
          pull.myCosmicKiss = pull.cosmicKissPattern[y < centers.y ? 1 : 3];
        }
      }
    }),
    raws({
      id: "M12S Hot-blooded Collect",
      type: "GainsEffect",
      netRegex: { effectId: "12A0", capture: true },
      condition: Condition.targetIsYou(),
      run: (pull, _hit) => pull.hasPyretic = true
    }),
    raws({
      id: "M12S Hot-blooded",
      type: "GainsEffect",
      netRegex: { effectId: "12A0", capture: true },
      condition: Condition.targetIsYou(),
      durationSeconds: (_pull, hit) => parseFloat(hit.duration),
      response: Response.stopMoving()
    }),
    raws({
      id: "M12S Idyllic Dream Lindwurm's Stone III",
      type: "StartsUsing",
      netRegex: { id: "B4F7", source: "Lindwurm", capture: true },
      condition: (pull) => {
        if (pull.hasPyretic)
          return false;
        if (pull.CanCleanse())
          return false;
        return true;
      },
      durationSeconds: (_pull, hit) => parseFloat(hit.castTime),
      suppressSeconds: 1,
      promise: async (pull) => {
        const combatantTwo = (await sayOverlayHandler({
          call: "getCombatants",
          names: [pull.me]
        })).combatants;
        const meBit = combatantTwo[0];
        if (combatantTwo.length !== 1 || meBit === void 0) {
          consol.error(
            `M12S Idyllic Dream Lindwurm's Stone III: Wrong combatants count ${combatantTwo.length}`
          );
          return;
        }
        const x = meBit.PosX;
        if (x < centers.x)
          pull.myPlatform = "west";
        else
          pull.myPlatform = "east";
      },
      infoText: (pull, _hit, voice) => {
        const patterns = pull.cosmicKissPattern;
        const platforms = pull.myPlatform;
        if (patterns.length !== 4 || platforms === void 0)
          return voice.avoidEarthTower({ dir: voice.south() });
        if (patterns[0] === "earth" && platforms === "west" || patterns[1] === "earth" && platforms === "east")
          return voice.avoidEarthTower({ dir: voice.in() });
        if (patterns[0] === "earth" && platforms === "east" || patterns[1] === "earth" && platforms === "west")
          return voice.avoidEarthTower({ dir: voice.southIn() });
      },
      outputStrings: {
        south: Voices.south,
        in: Voices.in,
        southIn: {
          en: "South + In",
          de: "S\xFCden + Rein",
          cn: "\u4E0B+\u5185",
          ko: "\uB0A8\uCABD + \uC548\uC73C\uB85C"
        },
        avoidEarthTower: {
          en: "${dir} (Avoid Earth Tower)",
          de: "${dir} (Vermeide Erd-Turm)",
          cn: "${dir} (\u907F\u5F00\u571F\u5854)",
          ko: "${dir} (\uB545 \uD0D1 \uD53C\uD558\uAE30)"
        }
      }
    }),
    raws({
      id: "M12S Doom Collect",
      type: "GainsEffect",
      netRegex: { effectId: "D24", capture: true },
      run: (pull, hit) => pull.doomPlayers.push(hit.target)
    }),
    raws({
      id: "M12S Doom Cleanse",
      type: "GainsEffect",
      netRegex: { effectId: "D24", capture: false },
      condition: (pull) => pull.CanCleanse(),
      delaySeconds: 0.1,
      suppressSeconds: 1,
      promise: async (pull) => {
        const combatantTwo = (await sayOverlayHandler({
          call: "getCombatants",
          names: [pull.me]
        })).combatants;
        const meBit = combatantTwo[0];
        if (combatantTwo.length !== 1 || meBit === void 0) {
          consol.error(
            `M12S Doom Cleanse: Wrong combatants count ${combatantTwo.length}`
          );
          return;
        }
        const x = meBit.PosX;
        if (x < centers.x)
          pull.myPlatform = "west";
        else
          pull.myPlatform = "east";
      },
      infoText: (pull, _hit, voice) => {
        const patterns = pull.cosmicKissPattern;
        const platforms = pull.myPlatform;
        let facingTwo;
        if (patterns.length !== 4 || platforms === void 0)
          facingTwo = "south";
        else if (patterns[0] === "earth" && platforms === "west" || patterns[1] === "earth" && platforms === "east")
          facingTwo = "in";
        else if (patterns[0] === "earth" && platforms === "east" || patterns[1] === "earth" && platforms === "west")
          facingTwo = "southIn";
        if (facingTwo === void 0)
          facingTwo = "south";
        const members = pull.doomPlayers;
        if (members.length > 2) {
          if (pull.hasPyretic)
            return voice.cleanseDooms();
          return voice.mech({
            cleanse: voice.cleanseDooms(),
            avoid: voice.avoidEarthTower({ dir: voice[facingTwo]() })
          });
        }
        if (members.length === 2) {
          const target1 = pull.party.member(pull.doomPlayers[0]);
          const target2 = pull.party.member(pull.doomPlayers[1]);
          if (pull.hasPyretic)
            return voice.cleanseDoom2({ target1, target2 });
          return voice.mech({
            cleanse: voice.cleanseDoom2({ target1, target2 }),
            avoid: voice.avoidEarthTower({ dir: voice[facingTwo]() })
          });
        }
        if (members.length === 1) {
          const target1 = pull.party.member(pull.doomPlayers[0]);
          if (pull.hasPyretic)
            return voice.cleanseDoom({ target: target1 });
          return voice.mech({
            cleanse: voice.cleanseDoom({ target: target1 }),
            avoid: voice.avoidEarthTower({ dir: voice[facingTwo]() })
          });
        }
      },
      outputStrings: {
        cleanseDooms: {
          en: "Cleanse Doom(s)",
          de: "Verh\xE4ngnis Reinigen",
          cn: "\u5EB7\u590D\u6B7B\u5BA3",
          ko: "\uC8FD\uC74C\uC758 \uC120\uACE0 \uC5D0\uC2A4\uB098"
        },
        cleanseDoom: {
          en: "Cleanse ${target}",
          de: "Reinige ${target}",
          fr: "Gu\xE9rison sur ${target}",
          cn: "\u5EB7\u590D ${target}",
          ko: "${target} \uC5D0\uC2A4\uB098",
          tc: "\u5EB7\u5FA9 ${target}"
        },
        cleanseDoom2: {
          en: "Cleanse ${target1}/${target2}",
          de: "Reinige ${target1}/${target2}",
          cn: "\u5EB7\u590D ${target1}/${target2}",
          ko: "${target1}/${target2} \uC5D0\uC2A4\uB098"
        },
        south: Voices.south,
        in: Voices.in,
        southIn: {
          en: "South + In",
          de: "S\xFCden + Rein",
          cn: "\u4E0B+\u5185",
          ko: "\uB0A8\uCABD + \uC548\uC73C\uB85C"
        },
        avoidEarthTower: {
          en: "${dir}",
          de: "${dir}",
          cn: "${dir}",
          ko: "${dir}"
        },
        mech: {
          en: "${cleanse} + ${avoid}",
          de: "${cleanse} + ${avoid}",
          cn: "${cleanse} + ${avoid}",
          ko: "${cleanse} + ${avoid}"
        }
      }
    }),
    raws({
      id: "M12S Avoid Earth Tower (Missing Dooms)",
      type: "Ability",
      netRegex: { id: "B4F6", capture: false },
      condition: (pull) => pull.CanCleanse(),
      delaySeconds: 0.5,
      suppressSeconds: 9999,
      promise: async (pull) => {
        const combatantTwo = (await sayOverlayHandler({
          call: "getCombatants",
          names: [pull.me]
        })).combatants;
        const meBit = combatantTwo[0];
        if (combatantTwo.length !== 1 || meBit === void 0) {
          consol.error(
            `M12S Doom Cleanse: Wrong combatants count ${combatantTwo.length}`
          );
          return;
        }
        const x = meBit.PosX;
        if (x < centers.x)
          pull.myPlatform = "west";
        else
          pull.myPlatform = "east";
      },
      infoText: (pull, _hit, voice) => {
        if (pull.doomPlayers[0] === void 0) {
          const patterns = pull.cosmicKissPattern;
          const platforms = pull.myPlatform;
          if (patterns.length !== 4 || platforms === void 0)
            return voice.avoidEarthTower({ dir: voice.south() });
          if (patterns[0] === "earth" && platforms === "west" || patterns[1] === "earth" && platforms === "east")
            return voice.avoidEarthTower({ dir: voice.in() });
          if (patterns[0] === "earth" && platforms === "east" || patterns[1] === "earth" && platforms === "west")
            return voice.avoidEarthTower({ dir: voice.southIn() });
        }
      },
      outputStrings: {
        south: Voices.south,
        in: Voices.in,
        southIn: {
          en: "South + In",
          de: "S\xFCden + Rein",
          cn: "\u4E0B+\u5185",
          ko: "\uB0A8\uCABD + \uC548\uC73C\uB85C"
        },
        avoidEarthTower: {
          en: "${dir} (Avoid Earth Tower)",
          de: "${dir} (Vermeide Erd-Turm)",
          cn: "${dir} (\u907F\u5F00\u571F\u5854)",
          ko: "${dir} (\uB545 \uD0D1 \uD53C\uD558\uAE30)"
        }
      }
    }),
    raws({
      id: "M12S Nearby and Faraway Portent",
      type: "GainsEffect",
      netRegex: { effectId: ["129E", "129F"], capture: true },
      condition: Condition.targetIsYou(),
      delaySeconds: (_pull, hit) => parseFloat(hit.duration) - 5.3,
      infoText: (pull, hit, voice) => {
        if (hit.effectId === "129E") {
          switch (pull.triggerSetConfig.portentStrategy) {
            case "dn":
              return voice.farOnYouWindDN();
            case "zenith":
              return voice.farOnYouWindZenith();
            case "nukemaru":
              return voice.farOnYouWindNukemaru();
          }
          return voice.farOnYouWind();
        }
        switch (pull.triggerSetConfig.portentStrategy) {
          case "dn":
            return voice.nearOnYouDarkDN();
          case "zenith":
            return voice.nearOnYouDarkZenith();
          case "nukemaru":
            return voice.nearOnYouDarkNukemaru();
        }
        return voice.nearOnYouDark();
      },
      outputStrings: {
        nearOnYouDarkDN: {
          en: "Near on YOU: Be on Hitbox N",
          de: "Nah auf DIR: Sei auf der Hitbox im Norden",
          cn: "\u8FD1\u70B9\u540D: \u7AD9\u4E0A\u8FB9\u5224\u5B9A\u5708",
          ko: "\uADFC\uAC70\uB9AC \uB300\uC0C1\uC790: \uD788\uD2B8\uBC15\uC2A4 \uBD81\uCABD\uC5D0 \uC11C\uAE30"
        },
        nearOnYouDarkZenith: {
          en: "Near on YOU: Be on Middle Hitbox (Lean North)",
          de: "Nah auf DIR: Sei auf der Hitbox in der Mitte (etwas n\xF6rdlich)",
          cn: "\u8FD1\u70B9\u540D: \u7AD9\u4E2D\u95F4\u5224\u5B9A\u5708 (\u504F\u4E0A)",
          ko: "\uADFC\uAC70\uB9AC \uB300\uC0C1\uC790: \uD788\uD2B8\uBC15\uC2A4 \uC911\uC559\uC5D0 \uC11C\uAE30 (\uC57D\uAC04 \uBD81\uCABD)"
        },
        nearOnYouDarkNukemaru: {
          en: "Near on YOU: Max Melee S (Near Outer Player)",
          de: "Nah auf DIR: Max Nahkampf im S\xFCden (Nahe \xE4u\xDFerem Spieler)",
          cn: "\u8FD1\u70B9\u540D: \u4E0B\u8FB9\u6700\u8FDC\u8FD1\u6218\u8DDD\u79BB (\u9760\u8FD1\u5916\u4FA7\u73A9\u5BB6)",
          ko: "\uADFC\uAC70\uB9AC \uB300\uC0C1\uC790: \uB0A8\uCABD \uCE7C\uB05D\uB51C (\uBC14\uAE65 \uD50C\uB808\uC774\uC5B4 \uAC00\uAE4C\uC774)"
        },
        nearOnYouDark: {
          en: "Dark: Near on YOU",
          de: "Dunkel: Nah auf DIR",
          cn: "\u6697: \u8FD1\u70B9\u540D",
          ko: "\uC5B4\uB460: \uADFC\uAC70\uB9AC \uB300\uC0C1\uC790"
        },
        farOnYouWindDN: {
          en: "Far on YOU: Be on Middle Hitbox",
          de: "Fern auf DIR: Sei auf der Mittleren Hitbox",
          cn: "\u8FDC\u70B9\u540D: \u7AD9\u4E2D\u95F4\u5224\u5B9A\u5708",
          ko: "\uC6D0\uAC70\uB9AC \uB300\uC0C1\uC790: \uD788\uD2B8\uBC15\uC2A4 \uC911\uC559\uC5D0 \uC11C\uAE30"
        },
        farOnYouWindZenith: {
          en: "Far on YOU: Max Melee N",
          de: "Fern auf DIR: Maximaler Nahkampf im Norden",
          cn: "\u8FDC\u70B9\u540D: \u4E0A\u8FB9\u6700\u8FDC\u8FD1\u6218\u8DDD\u79BB",
          ko: "\uC6D0\uAC70\uB9AC \uB300\uC0C1\uC790: \uBD81\uCABD \uCE7C\uB05D\uB51C"
        },
        farOnYouWindNukemaru: {
          en: "Far on YOU: Be on Hitbox S",
          de: "Fern auf DIR: Auf der Hitbox im S\xFCden",
          cn: "\u8FDC\u70B9\u540D: \u7AD9\u4E0B\u8FB9\u5224\u5B9A\u5708",
          ko: "\uC6D0\uAC70\uB9AC \uB300\uC0C1\uC790: \uD788\uD2B8\uBC15\uC2A4 \uB0A8\uCABD\uC5D0 \uC11C\uAE30"
        },
        farOnYouWind: {
          en: "Wind: Far on YOU",
          de: "Wind: Fern auf DIR",
          cn: "\u98CE: \u8FDC\u70B9\u540D",
          ko: "\uBC14\uB78C: \uC6D0\uAC70\uB9AC \uB300\uC0C1\uC790"
        }
      }
    }),
    raws({
      id: "M12S Nearby and Faraway Portent Baits",
      type: "GainsEffect",
      netRegex: { effectId: ["129E", "129F"], capture: true },
      condition: (pull) => pull.hasLightResistanceDown,
      delaySeconds: (_pull, hit) => parseFloat(hit.duration) - 5.3,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        if (pull.hasPyretic) {
          switch (pull.triggerSetConfig.portentStrategy) {
            case "dn":
              return voice.baitFireDN();
            case "zenith":
              return voice.baitFireZenith();
            case "nukemaru":
              return voice.baitFireNukemaru();
          }
          return voice.baitFire();
        }
        switch (pull.triggerSetConfig.portentStrategy) {
          case "dn":
            return voice.baitEarthDN();
          case "zenith":
            return voice.baitEarthZenith();
          case "nukemaru":
            return voice.baitEarthNukemaru();
        }
        return voice.baitEarth();
      },
      outputStrings: {
        baitFireDN: {
          en: "Bait Cone N Center Below Dark/S Center",
          de: "K\xF6der Kegel-AoE Norden Mitte Darunter Dunkel/S\xFCden Mitte",
          cn: "\u8BF1\u5BFC\u6247\u5F62: \u5317\u4FA7\u4E2D\u5FC3, \u6697\u4E0B\u65B9/\u5357\u4FA7\u4E2D\u5FC3",
          ko: "\uBD80\uCC44\uAF34 \uC720\uB3C4 \uBD81\uCABD \uC5B4\uB460 \uBC11/\uB0A8\uCABD \uC911\uC559"
        },
        baitFireZenith: {
          en: "Bait Cone S, Max Melee",
          de: "K\xF6der Kegel-AoE S, Max Nahkampf",
          cn: "\u8BF1\u5BFC\u6247\u5F62: \u5357\u4FA7, \u6700\u8FDC\u8FD1\u6218\u8DDD\u79BB",
          ko: "\uBD80\uCC44\uAF34 \uC720\uB3C4 \uB0A8\uCABD, \uCE7C\uB05D\uB51C"
        },
        baitFireNukemaru: {
          en: "Bait Cone, N of Platform/S Max Melee",
          de: "K\xF6der Kegel-AoE, Norden der Plattform/ S\xFCden Max Nahkampf",
          cn: "\u8BF1\u5BFC\u6247\u5F62: \u5E73\u53F0\u5317\u4FA7/\u5357\u4FA7\u6700\u8FDC\u8FD1\u6218\u8DDD\u79BB",
          ko: "\uBD80\uCC44\uAF34 \uC720\uB3C4, \uD50C\uB7AB\uD3FC \uBD81\uCABD/\uB0A8\uCABD \uCE7C\uB05D\uB51C"
        },
        baitFire: {
          en: "Fire: Bait Cone",
          de: "Feuer: K\xF6der Kegel-AoE",
          cn: "\u706B: \u8BF1\u5BFC\u6247\u5F62",
          ko: "\uBD88: \uBD80\uCC44\uAF34 \uC720\uB3C4"
        },
        baitEarthDN: {
          en: "Bait Cone N Center Below Dark/S Center",
          de: "K\xF6der Kegel-AoE Norden Mitte Darunter Dunkel/S\xFCden Mitte",
          cn: "\u8BF1\u5BFC\u6247\u5F62: \u5317\u4FA7\u4E2D\u5FC3, \u6697\u4E0B\u65B9/\u5357\u4FA7\u4E2D\u5FC3",
          ko: "\uBD80\uCC44\uAF34 \uC720\uB3C4 \uBD81\uCABD \uC5B4\uB460 \uBC11/\uB0A8\uCABD \uC911\uC559"
        },
        baitEarthZenith: {
          en: "Bait Cone Middle, Max Melee (Lean North)",
          de: "K\xF6der Kegel-AoE Mitte, Max Nahkampf (etwas n\xF6rdlich)",
          cn: "\u8BF1\u5BFC\u6247\u5F62: \u4E2D\u95F4, \u6700\u8FDC\u8FD1\u6218\u8DDD\u79BB (\u504F\u5317)",
          ko: "\uBD80\uCC44\uAF34 \uC720\uB3C4 \uC911\uC559, \uCE7C\uB05D\uB51C (\uC57D\uAC04 \uBD81\uCABD)"
        },
        baitEarthNukemaru: {
          en: "Bait Cone, S Max Melee/N of Platform",
          de: "K\xF6der Kegel-AoE, S\xFCden Max Nahkampf/ Norden der Plattform",
          cn: "\u8BF1\u5BFC\u6247\u5F62: \u5357\u4FA7\u6700\u8FDC\u8FD1\u6218\u8DDD\u79BB/\u5E73\u53F0\u5317\u4FA7",
          ko: "\uBD80\uCC44\uAF34 \uC720\uB3C4, \uB0A8\uCABD \uCE7C\uB05D\uB51C/\uD50C\uB7AB\uD3FC \uBD81\uCABD"
        },
        baitEarth: {
          en: "Earth: Bait Cone",
          de: "Erde: K\xF6der Kegel-AoE",
          cn: "\u571F: \u8BF1\u5BFC\u6247\u5F62",
          ko: "\uB545: \uBD80\uCC44\uAF34 \uC720\uB3C4"
        }
      }
    }),
    raws({
      id: "M12S Temporal Curtain Part 1 Collect",
      type: "Ability",
      netRegex: { id: "B51D", source: "Lindschrat", capture: true },
      run: (pull, hit) => {
        switch (hit.sourceId) {
          case pull.idyllicDreamActorEW:
            pull.idyllicVision8SafeSides = "frontBack";
            return;
          case pull.idyllicDreamActorNS:
            pull.idyllicVision8SafeSides = "sides";
        }
      }
    }),
    raws({
      id: "M12S Temporal Curtain Part 1",
      type: "Ability",
      netRegex: { id: "B51D", source: "Lindschrat", capture: true },
      infoText: (pull, hit, voice) => {
        switch (hit.sourceId) {
          case pull.idyllicDreamActorEW:
            return voice.frontBackLater();
          case pull.idyllicDreamActorNS:
            return voice.sidesLater();
        }
      },
      outputStrings: {
        frontBackLater: {
          en: "Portal + Under Boss (later)",
          de: "Portal + Unter den Boss (sp\xE4ter)",
          cn: "\u4F20\u9001 + Boss\u811A\u4E0B (\u7A0D\u540E)",
          ko: "\uD3EC\uD0C8 + \uC55E/\uB4A4 \uBD84\uC2E0 (\uB098\uC911\uC5D0)"
        },
        sidesLater: {
          en: "Portal + E/W of Clone (later)",
          de: "Portal + O/W vom Klon (sp\xE4ter)",
          cn: "\u4F20\u9001 + \u5206\u8EAB\u5DE6/\u53F3 (\u7A0D\u540E)",
          ko: "\uD3EC\uD0C8 + \uC591 \uC606 \uBD84\uC2E0 (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M12S Temporal Curtain Part 2 Collect",
      type: "AbilityExtra",
      netRegex: { id: "B4D9", capture: true },
      run: (pull, hit) => {
        switch (hit.sourceId) {
          case pull.idyllicDreamActorEW:
            pull.idyllicVision7SafeSides = "frontBack";
            return;
          case pull.idyllicDreamActorNS:
            pull.idyllicVision7SafeSides = "sides";
            return;
          case pull.idyllicDreamActorSnaking: {
            const x = parseFloat(hit.x);
            pull.idyllicVision7SafePlatform = x < 100 ? "east" : "west";
          }
        }
      }
    }),
    raws({
      id: "M12S Temporal Curtain Part 2",
      type: "AbilityExtra",
      netRegex: { id: "B4D9", capture: false },
      infoText: (pull, _hit, voice) => {
        if (pull.idyllicVision7SafeSides === "frontBack") {
          if (pull.idyllicVision7SafePlatform === "east")
            return voice.frontBackEastLater();
          if (pull.idyllicVision7SafePlatform === "west")
            return voice.frontBackWestLater();
        }
        if (pull.idyllicVision7SafeSides === "sides") {
          if (pull.idyllicVision7SafePlatform === "east")
            return voice.sidesEastLater();
          if (pull.idyllicVision7SafePlatform === "west")
            return voice.sidesWestLater();
        }
      },
      outputStrings: {
        frontBackWestLater: {
          en: "West Platform => N/S of Clone (later)",
          de: "Westen Platform => N/S des Klones (sp\xE4ter)",
          cn: "\u5DE6\u5E73\u53F0 + \u5206\u8EAB\u4E0A/\u4E0B (\u7A0D\u540E)",
          ko: "\uC11C\uCABD \uD50C\uB7AB\uD3FC => \uC55E/\uB4A4 \uBD84\uC2E0 (\uB098\uC911\uC5D0)"
        },
        sidesWestLater: {
          en: "West Platform => Under Boss (later)",
          de: "Westen Platform => Unter den Boss (sp\xE4ter)",
          cn: "\u5DE6\u5E73\u53F0 + Boss\u811A\u4E0B (\u7A0D\u540E)",
          ko: "\uC11C\uCABD \uD50C\uB7AB\uD3FC => \uC591 \uC606 \uBD84\uC2E0 (\uB098\uC911\uC5D0)"
        },
        frontBackEastLater: {
          en: "East Platform => N/S of Clone (later)",
          de: "Ost Platform => N/S des Klones (sp\xE4ter)",
          cn: "\u53F3\u5E73\u53F0 + \u5206\u8EAB\u4E0A/\u4E0B (\u7A0D\u540E)",
          ko: "\uB3D9\uCABD \uD50C\uB7AB\uD3FC => \uC55E/\uB4A4 \uBD84\uC2E0 (\uB098\uC911\uC5D0)"
        },
        sidesEastLater: {
          en: "East Platform => Under Boss (later)",
          de: "Ost Platform => Unter den Boss (sp\xE4ter)",
          cn: "\u53F3\u5E73\u53F0 + Boss\u811A\u4E0B (\u7A0D\u540E)",
          ko: "\uB3D9\uCABD \uD50C\uB7AB\uD3FC => \uC591 \uC606 \uBD84\uC2E0 (\uB098\uC911\uC5D0)"
        }
      }
    }),
    raws({
      id: "M12S Twisted Vision 6 Light Party Stacks",
      type: "Ability",
      netRegex: { id: "BBE2", source: "Lindwurm", capture: false },
      condition: (pull) => pull.twistedVisionCounter === 6,
      alertText: (pull, _hit, voice) => {
        const lead = pull.replication3CloneOrder[0];
        if (lead === void 0)
          return;
        const facingDigitSequence = lead % 2 === 0 ? [0, 2, 4, 6] : [1, 3, 5, 7];
        const abilitie = pull.replication4AbilityOrder.splice(0, 4);
        const pileFacings = [];
        let i = 0;
        for (const facingDigit of facingDigitSequence) {
          if (abilitie[i++] === headSignState["heavySlamTether"])
            pileFacings.push(facingDigit);
        }
        const facingNum1 = pileFacings[0];
        const facingNum2 = pileFacings[1];
        if (facingNum1 === void 0 || facingNum2 === void 0) {
          return lead % 2 === 0 ? voice.cardinals() : voice.intercards();
        }
        const dir1s = Facings.output8Dir[facingNum1] ?? "unknown";
        const dir2s = Facings.output8Dir[facingNum2] ?? "unknown";
        return voice.stack({ dir1: voice[dir1s](), dir2: voice[dir2s]() });
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        cardinals: Voices.cardinals,
        intercards: Voices.intercards,
        stack: {
          en: "Stack ${dir1}/${dir2} + Lean Middle Out",
          de: "Sammeln ${dir1}/${dir2} + Etwas Mittig Au\xDFen",
          cn: "${dir1}/${dir2}\u5206\u644A + \u504F\u5411\u4E2D\u95F4\u5916\u4FA7",
          ko: "${dir1}/${dir2} \uC250\uC5B4 + \uC911\uC559 \uBC16\uC73C\uB85C \uC57D\uAC04 \uBE7C\uAE30"
        }
      }
    }),
    raws({
      id: "M12S Twisted Vision 7 Safe Platform",
      type: "StartsUsing",
      netRegex: { id: "BBE2", source: "Lindwurm", capture: true },
      condition: (pull) => pull.twistedVisionCounter === 7,
      durationSeconds: (_pull, hit) => parseFloat(hit.castTime) + 4.5,
      infoText: (pull, _hit, voice) => {
        if (pull.idyllicVision7SafeSides === "frontBack") {
          if (pull.idyllicVision7SafePlatform === "east")
            return voice.frontBackEastPlatform();
          if (pull.idyllicVision7SafePlatform === "west")
            return voice.frontBackWestPlatform();
        }
        if (pull.idyllicVision7SafeSides === "sides") {
          if (pull.idyllicVision7SafePlatform === "east")
            return voice.sidesEastPlatform();
          if (pull.idyllicVision7SafePlatform === "west")
            return voice.sidesWestPlatform();
        }
        return voice.safePlatform();
      },
      outputStrings: {
        safePlatform: {
          en: "Move to Safe Platform Side => Dodge Cleaves",
          de: "Geh zur sicheren Platform-Seite => Cleave ausweichen",
          cn: "\u79FB\u52A8\u5230\u5B89\u5168\u5E73\u53F0\u4FA7 => \u907F\u5F00\u6247\u5F62",
          ko: "\uC548\uC804\uD55C \uD50C\uB7AB\uD3FC \uCABD\uC73C\uB85C \uC774\uB3D9 => \uBD80\uCC44\uAF34 \uD53C\uD558\uAE30"
        },
        sidesWestPlatform: {
          en: "West Platform => Under Boss",
          de: "Westen Platform => Unter den Boss",
          cn: "\u5DE6\u5E73\u53F0 + Boss\u811A\u4E0B",
          ko: "\uC11C\uCABD \uD50C\uB7AB\uD3FC => \uBCF4\uC2A4 \uBC11"
        },
        sidesEastPlatform: {
          en: "East Platform => Under Boss",
          de: "Ost Platform => Unter den Boss",
          cn: "\u53F3\u5E73\u53F0 + Boss\u811A\u4E0B",
          ko: "\uB3D9\uCABD \uD50C\uB7AB\uD3FC => \uBCF4\uC2A4 \uBC11"
        },
        frontBackEastPlatform: {
          en: "East Platform => N/S of Clone",
          de: "Ost Platform => N/S des Klones",
          cn: "\u53F3\u5E73\u53F0 + \u5206\u8EAB\u4E0A/\u4E0B",
          ko: "\uB3D9\uCABD \uD50C\uB7AB\uD3FC => \uBD84\uC2E0 \uB0A8/\uBD81"
        },
        frontBackWestPlatform: {
          en: "West Platform => N/S of Clone",
          de: "Westen Platform => N/S des Klones",
          cn: "\u5DE6\u5E73\u53F0 + \u5206\u8EAB\u4E0A/\u4E0B",
          ko: "\uC11C\uCABD \uD50C\uB7AB\uD3FC => \uBD84\uC2E0 \uB0A8/\uBD81"
        }
      }
    }),
    raws({
      id: "M12S Twisted Vision 8 Light Party Stacks",
      type: "StartsUsing",
      netRegex: { id: "BBE2", source: "Lindwurm", capture: false },
      condition: (pull) => pull.twistedVisionCounter === 8,
      alertText: (pull, _hit, voice) => {
        const lead = pull.replication3CloneOrder[0];
        if (lead === void 0)
          return;
        const facingDigitSequence = lead % 2 !== 0 ? [0, 2, 4, 6] : [1, 3, 5, 7];
        const abilitie = pull.replication4AbilityOrder.slice(4, 8);
        const pileFacings = [];
        let i = 0;
        for (const facingDigit of facingDigitSequence) {
          if (abilitie[i++] === headSignState["heavySlamTether"])
            pileFacings.push(facingDigit);
        }
        const facingNum1 = pileFacings[0];
        const facingNum2 = pileFacings[1];
        if (facingNum1 === void 0 || facingNum2 === void 0) {
          return lead % 2 !== 0 ? voice.cardinals() : voice.intercards();
        }
        const dir1s = Facings.output8Dir[facingNum1] ?? "unknown";
        const dir2s = Facings.output8Dir[facingNum2] ?? "unknown";
        return voice.stack({ dir1: voice[dir1s](), dir2: voice[dir2s]() });
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        cardinals: Voices.cardinals,
        intercards: Voices.intercards,
        stack: {
          en: "Stack ${dir1}/${dir2} + Lean Middle Out",
          de: "Sammeln ${dir1}/${dir2} + Etwas Mittig Au\xDFen",
          cn: "${dir1}/${dir2}\u5206\u644A + \u504F\u5411\u4E2D\u95F4\u5916\u4FA7",
          ko: "${dir1}/${dir2} \uC250\uC5B4 + \uC911\uC559 \uBC14\uAE65\uCABD\uC73C\uB85C \uBE7C\uAE30"
        }
      }
    }),
    raws({
      id: "M12S Twisted Vision 8 Dodge Cleaves",
      type: "Ability",
      netRegex: { id: "BE5D", source: "Lindwurm", capture: false },
      condition: (pull) => pull.twistedVisionCounter === 8,
      alertText: (pull, _hit, voice) => {
        if (pull.idyllicVision8SafeSides === "sides")
          return voice.sides();
        if (pull.idyllicVision8SafeSides === "frontBack")
          return voice.frontBack();
      },
      run: (pull) => {
        delete pull.idyllicVision8SafeSides;
      },
      outputStrings: {
        sides: {
          en: "E/W of Clone",
          de: "O/W des Klones",
          cn: "\u5206\u8EAB\u4E1C/\u897F",
          ko: "\uBD84\uC2E0 \uB3D9/\uC11C"
        },
        frontBack: {
          en: "Under Boss",
          de: "Unter den Boss",
          cn: "Boss\u811A\u4E0B",
          ko: "\uBCF4\uC2A4 \uBC11"
        }
      }
    }),
    whenChant("B533").aoe().hold(4.7).cooldown(9999),
    whenChant("B535").by("Lindschrat").info("big AoE!").hold(4.7).cooldown(9999),
  ],
});
