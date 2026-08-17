var Tool = {
  isHealerJob: function (j) { return j === "WHM" || j === "SCH" || j === "AST" || j === "SGE"; },
  isMeleeDpsJob: function (j) { return j === "MNK" || j === "DRG" || j === "NIN" || j === "SAM" || j === "RPR" || j === "VPR"; },
  isCasterDpsJob: function (j) { return j === "BLM" || j === "SMN" || j === "RDM" || j === "PCT" || j === "BLU"; },
};

const centerXBit = 100;
const centerYBit = 100;
const readRelativeHdg = (toXBit, toYBit, fromXBit, fromYBit) => {
  const deltaXBit = toXBit - fromXBit;
  const deltaYBit = toYBit - fromYBit;
  return Math.atan2(deltaXBit, deltaYBit);
};
const jumpSite = [
  { jump: "dirW", x: 86, y: 100, safe: "dirE" },
  { jump: "dirE", x: 114, y: 100, safe: "dirW" },
  { jump: "dirN", x: 100, y: 86, safe: "dirS" },
  { jump: "dirS", x: 100, y: 114, safe: "dirN" }
];
const cageSetup = (id) => {
  const digitWord = id.replace("gaolOrder", "");
  return {
    id,
    name: {
      en: `Titan Gaol Order ${digitWord}`,
      de: `Titan Gef\xE4ngnis Reihenfolge ${digitWord}`,
      fr: `Ordre ge\xF4le de Titan ${digitWord}`,
      ja: `\u30B8\u30A7\u30A4\u30EB\u306E\u9806\u756A ${digitWord}`,
      cn: `\u6CF0\u5766\u77F3\u7262\u987A\u5E8F ${digitWord}`,
      ko: `\uB3CC\uAC10\uC625 \uC21C\uC11C ${digitWord}`,
      tc: `\u6CF0\u5766\u77F3\u7262\u9806\u5E8F ${digitWord}`
    },
    type: "string",
    default: ""
  };
};
const cuePut = {
  id: "TheWeaponsRefrainUltimate",
  zoneId: 777,
  config: [

    {
      ...cageSetup("gaolOrder1"),
      comment: {
        en: 'Each entry can be the three letter job (e.g. "war" or "SGE") or the full name (e.g. "Tini Poutini"), all case insensitive. Smaller numbers will be listed first in the gaol order. Duplicate jobs will sort players alphabetically. Anybody not listed will be added to the end alphabetically. Blank entries are ignored. If players are listed multiple times by name or job, the lower number will be considered.',
        de: 'Jeder Eintrag kann aus drei Buchstaben des Jobs bestehen (z. B. "war" oder "SGE") oder aus dem vollst\xE4ndigen Namen (z. B. "Tini Poutini"), wobei Gro\xDF- und Kleinschreibung nicht ber\xFCcksichtigt werden. Kleinere Nummern werden in der Reihenfolge der Gef\xE4ngnisse zuerst aufgef\xFChrt. Bei doppelten Auftr\xE4gen werden die Spieler alphabetisch sortiert. Jeder nicht aufgef\xFChrte Spieler wird am Ende alphabetisch eingeordnet. Leere Eintr\xE4ge werden ignoriert. Wenn Spieler mehrfach nach Namen oder Beruf aufgelistet sind, wird die niedrigere Nummer ber\xFCcksichtigt.',
        fr: `Chaque entr\xE9e peut \xEAtre d\xE9sign\xE9 par les jobs en trois lettres (par exemple "war" ou "SGE") ou le nom complet (par exemple "Tini Poutini"), sans tenir compte des majuscules et minuscules. Les plus petits num\xE9ros seront class\xE9s en premier dans l'ordre des ge\xF4les. Les doublons seront class\xE9s par ordre alphab\xE9tique. Toute personne ne figurant pas sur la liste sera ajout\xE9e \xE0 la fin par ordre alphab\xE9tique. Les entr\xE9es vides sont ignor\xE9es. Si des joueurs sont list\xE9s plusieurs fois par nom ou par fonction, le num\xE9ro le plus bas sera pris en compte.`,
        ja: '\u5404\u9805\u76EE\u306F\u30013\u6587\u5B57\u306E\u30B8\u30E7\u30D6\u540D\uFF08\u4F8B: "war" \u307E\u305F\u306F "SGE"\uFF09\u307E\u305F\u306F\u30D5\u30EB\u30CD\u30FC\u30E0\uFF08\u4F8B: "Tini Poutini"\uFF09\u306E\u3044\u305A\u308C\u304B\u3092\u5165\u529B\u3067\u304D\u307E\u3059\u3002\u5927\u6587\u5B57\u5C0F\u6587\u5B57\u306F\u533A\u5225\u3055\u308C\u307E\u305B\u3093\u3002\u756A\u53F7\u306E\u5C0F\u3055\u3044\u9806\u306B\u30B8\u30A7\u30A4\u30EB\u306E\u9806\u756A\u30EA\u30B9\u30C8\u306B\u767B\u9332\u3055\u308C\u307E\u3059\u3002\u91CD\u8907\u3059\u308B\u30B8\u30E7\u30D6\u306F\u540D\u524D\u9806\u306B\u4E26\u3079\u66FF\u3048\u3089\u308C\u307E\u3059\u3002\u30EA\u30B9\u30C8\u3055\u308C\u3066\u3044\u306A\u3044\u30D7\u30EC\u30A4\u30E4\u30FC\u306F\u540D\u524D\u9806\u306B\u6700\u5F8C\u306B\u8FFD\u52A0\u3055\u308C\u307E\u3059\u3002\u7A7A\u767D\u306E\u9805\u76EE\u306F\u7121\u8996\u3055\u308C\u307E\u3059\u3002\u30D7\u30EC\u30A4\u30E4\u30FC\u304C\u540D\u524D\u307E\u305F\u306F\u30B8\u30E7\u30D6\u3067\u8907\u6570\u56DE\u767B\u9332\u3055\u308C\u3066\u3044\u308B\u5834\u5408\u3001\u5C0F\u3055\u3044\u307B\u3046\u306E\u756A\u53F7\u304C\u4F7F\u7528\u3055\u308C\u307E\u3059\u3002',
        cn: '\u6BCF\u4E2A\u6761\u76EE\u53EF\u4EE5\u662F\u4E09\u4E2A\u5B57\u6BCD\u7684\u804C\u4E1A\u7F29\u5199 (\u4F8B\u5982 "war" \u6216  "SGE") \u6216\u73A9\u5BB6\u5168\u540D\uFF08\u4F8B\u5982 "Tini Poutini"\uFF09\uFF0C\u6240\u6709\u5B57\u6BCD\u4E0D\u533A\u5206\u5927\u5C0F\u5199\u3002\u7F16\u53F7\u8F83\u5C0F\u7684\u5C06\u5728\u77F3\u7262\u987A\u5E8F\u4E2D\u6392\u5217\u5728\u524D\u3002\u91CD\u590D\u7684\u804C\u4E1A\u5C06\u6309\u59D3\u540D\u5B57\u6BCD\u987A\u5E8F\u5BF9\u73A9\u5BB6\u8FDB\u884C\u6392\u5E8F\u3002\u672A\u5217\u51FA\u7684\u961F\u5458\u5C06\u6309\u5B57\u6BCD\u987A\u5E8F\u6DFB\u52A0\u5230\u672B\u5C3E\u3002\u7A7A\u767D\u6761\u76EE\u5C06\u88AB\u5FFD\u7565\u3002\u5982\u679C\u73A9\u5BB6\u6309\u59D3\u540D\u6216\u804C\u4E1A\u88AB\u591A\u6B21\u5217\u51FA\uFF0C\u5219\u4EE5\u8F83\u5C0F\u7F16\u53F7\u4E3A\u51C6\u3002',
        ko: '\uAC01 \uD56D\uBAA9\uC5D0\uB294 \uB300\uC18C\uBB38\uC790\uB97C \uAD6C\uBD84\uD558\uC9C0 \uC54A\uB294 \uC138 \uAE00\uC790 \uC9C1\uC5C5\uBA85(\uC608: "war" \uB610\uB294 "SGE") \uB610\uB294 \uC804\uCCB4 \uC774\uB984(\uC608: "\uBE5B\uC758\uC804\uC0AC")\uC744 \uC785\uB825\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4. \uBA3C\uC800 \uC785\uB825\uB41C \uD56D\uBAA9\uC774 \uAC10\uC625 \uC21C\uC11C\uC5D0\uC11C \uBA3C\uC800 \uB098\uC5F4\uB429\uB2C8\uB2E4. \uC9C1\uC5C5\uC774 \uC911\uBCF5\uB41C \uACBD\uC6B0\uC5D0\uB294  \uC54C\uD30C\uBCB3 \uC21C(\uAC00\uB098\uB2E4 \uC21C)\uC73C\uB85C \uB098\uD0C0\uB0A9\uB2C8\uB2E4. \uBAA9\uB85D\uC5D0 \uC5C6\uB294 \uC0AC\uB78C\uC740 \uC54C\uD30C\uBCB3 \uC21C\uC73C\uB85C \uB9E8 \uB05D\uC5D0 \uCD94\uAC00\uB429\uB2C8\uB2E4. \uBE48 \uCE78\uC740 \uBB34\uC2DC\uB429\uB2C8\uB2E4. \uD50C\uB808\uC774\uC5B4\uAC00 \uC774\uB984 \uB610\uB294 \uC9C1\uC5C5\uBCC4\uB85C \uC5EC\uB7EC \uBC88 \uB098\uC5F4\uB41C \uACBD\uC6B0, \uBA3C\uC800 \uC785\uB825\uB41C \uD56D\uBAA9\uC774 \uC0AC\uC6A9\uB429\uB2C8\uB2E4.',
        tc: '\u6BCF\u500B\u689D\u76EE\u53EF\u4EE5\u662F\u4E09\u500B\u5B57\u6BCD\u7684\u8077\u696D\u7E2E\u5BEB (\u4F8B\u5982 "war" \u6216  "SGE") \u6216\u73A9\u5BB6\u5168\u540D\uFF08\u4F8B\u5982 "Tini Poutini"\uFF09\uFF0C\u6240\u6709\u5B57\u6BCD\u4E0D\u5340\u5206\u5927\u5C0F\u5BEB\u3002\u7DE8\u865F\u8F03\u5C0F\u7684\u5C07\u5728\u77F3\u7262\u9806\u5E8F\u4E2D\u6392\u5217\u5728\u524D\u3002\u91CD\u8907\u7684\u8077\u696D\u5C07\u6309\u59D3\u540D\u5B57\u6BCD\u9806\u5E8F\u5C0D\u73A9\u5BB6\u9032\u884C\u6392\u5E8F\u3002\u672A\u5217\u51FA\u7684\u968A\u54E1\u5C07\u6309\u5B57\u6BCD\u9806\u5E8F\u6DFB\u52A0\u5230\u672B\u5C3E\u3002\u7A7A\u767D\u689D\u76EE\u5C07\u88AB\u5FFD\u7565\u3002\u5982\u679C\u73A9\u5BB6\u6309\u59D3\u540D\u6216\u8077\u696D\u88AB\u591A\u6B21\u5217\u51FA\uFF0C\u5247\u4EE5\u8F03\u5C0F\u7DE8\u865F\u70BA\u6E96\u3002'
      }
    },
    cageSetup("gaolOrder2"),
    cageSetup("gaolOrder3"),
    cageSetup("gaolOrder4"),
    cageSetup("gaolOrder5"),
    cageSetup("gaolOrder6"),
    cageSetup("gaolOrder7"),
    cageSetup("gaolOrder8"),
    cageSetup("gaolOrder9"),
    cageSetup("gaolOrder10"),
    cageSetup("gaolOrder11"),
    cageSetup("gaolOrder12"),
    cageSetup("gaolOrder13"),
    cageSetup("gaolOrder14"),
    cageSetup("gaolOrder15"),
    cageSetup("gaolOrder16"),
    cageSetup("gaolOrder17"),
    cageSetup("gaolOrder18"),
    cageSetup("gaolOrder19"),
    cageSetup("gaolOrder20")

  ],
  timelineFile: "ultima_weapon_ultimate.txt",
  initData: () => {
    return {
      combatantData: [],
      phase: "garuda",
      bossId: {},
      garudaAwoken: false,
      ifritAwoken: false,
      thermalLow: {},
      beyondLimits:  new Set(),
      slipstreamCount: 0,
      nailAdds: [],
      nailDeaths: {},
      nailDeathOrder: [],
      ifritUntargetableCount: 0,
      seenTitanFirstJump: false,
      titanGaols: [],
      titanBury: [],
      ifritRadiantPlumeLocations: [],
      possibleIfritIDs: []
    };
  },
  timelineTriggers: [
    {
      id: "UWU Diffractive Laser",
      regex: /Diffractive Laser/,
      beforeSeconds: 5,
      suppressSeconds: 3,
      response: Response.tankCleave()
    },
    {
      id: "UWU Feather Rain",
      regex: /Feather Rain/,
      beforeSeconds: 3,
      suppressSeconds: 3,
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Move!",
          de: "Bewegen",
          fr: "Bougez !",
          ja: "\u30D5\u30A7\u30B6\u30FC\u30EC\u30A4\u30F3",
          cn: "\u8EB2\u7FBD\u6BDB",
          ko: "\uC774\uB3D9",
          tc: "\u8EB2\u7FBD\u6BDB"
        }
      }
    },
    {
      id: "UWU Eruption",
      regex: /Eruption 1/,
      beforeSeconds: 10,
      condition: (pull) => pull.phase !== "suppression",
      alertText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Eruption Baits",
          de: "K\xF6der Eruption",
          fr: "Attirez les \xE9ruptions",
          ja: "\u30A8\u30E9\u30D7\u30B7\u30E7\u30F3",
          cn: "\u8BF1\u5BFC\u5730\u706B",
          ko: "\uC6A9\uC554 \uBD84\uCD9C \uC720\uB3C4",
          tc: "\u8A98\u5C0E\u5674\u767C"
        }
      }
    }
  ],
  triggers: [

    {
      id: "UWU Phase Tracker",
      type: "Ability",

      netRegex: { id: ["2B53", "2B5F", "2CFD", "2CF5", "2B87", "2D4C", "2D4D"] },
      run: (pull, hit) => {
        if (pull.phase === "garuda" && hit.id === "2B53") {
          pull.bossId.garuda = hit.sourceId;
        } else if (pull.phase === "garuda" && hit.id === "2B5F") {
          pull.phase = "ifrit";
          pull.bossId.ifrit = hit.sourceId;
        } else if (pull.phase === "ifrit" && hit.id === "2CFD") {
          pull.phase = "titan";
          pull.bossId.titan = hit.sourceId;
        } else if (pull.phase === "titan" && hit.id === "2CF5") {
          pull.phase = "intermission";
        } else if (pull.phase === "intermission" && hit.id === "2B87") {
          pull.phase = "predation";
          pull.bossId.ultima = hit.sourceId;
        } else if (hit.id === "2D4C") {
          pull.phase = "annihilation";
        } else if (hit.id === "2D4D") {
          pull.phase = "suppression";
        }
      }
    },
    {

      id: "UWU Phase Tracker Finale",
      type: "Ability",
      netRegex: { source: "The Ultima Weapon", id: "2D4D", capture: false },
      delaySeconds: 74,
      run: (pull) => pull.phase = "finale"
    },
    {
      id: "UWU Garuda Woken",
      type: "GainsEffect",
      netRegex: { target: "Garuda", effectId: "5F9", capture: false },
      sound: "Long",
      run: (pull) => pull.garudaAwoken = true
    },
    {
      id: "UWU Ifrit Woken",
      type: "GainsEffect",
      netRegex: { target: "Ifrit", effectId: "5F9", capture: false },
      sound: "Long",
      run: (pull) => pull.ifritAwoken = true
    },
    {
      id: "UWU Titan Woken",
      type: "GainsEffect",
      netRegex: { target: "Titan", effectId: "5F9", capture: false },
      sound: "Long"
    },
    {
      id: "UWU Thermal Low Gain",
      type: "GainsEffect",
      netRegex: { effectId: "5F5" },
      run: (pull, hit) => pull.thermalLow[hit.target] = parseInt(hit.count)
    },
    {
      id: "UWU Thermal Low Lose",
      type: "LosesEffect",
      netRegex: { effectId: "5F5" },
      run: (pull, hit) => pull.thermalLow[hit.target] = 0
    },
    {
      id: "UWU Beyond Limits Gain",
      type: "GainsEffect",
      netRegex: { effectId: "5FA" },
      run: (pull, hit) => pull.beyondLimits.add(hit.target)
    },
    {
      id: "UWU Beyond Limits Lose",
      type: "LosesEffect",
      netRegex: { effectId: "5FA" },
      run: (pull, hit) => pull.beyondLimits.delete(hit.target)
    },

    {
      id: "UWU Garuda Slipstream",
      type: "StartsUsing",
      netRegex: { id: "2B53", source: "Garuda", capture: false },
      response: Response.getBehind(),
      run: (pull) => pull.slipstreamCount++
    },
    {
      id: "UWU Garuda Downburst",

      type: "Ability",
      netRegex: { id: "2B53", source: "Garuda", capture: false },
      delaySeconds: (pull) => pull.slipstreamCount === 4 ? 10 : 0,
      suppressSeconds: 3,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {

          tankCleave: Voices.tankCleave,
          partyStack: Voices.stackMarker,
          tankCleavePartyOut: {
            en: "Tank Cleave (PARTY OUT)",
            de: "Tank Cleave (GRUPPE RAUS)",
            fr: "Tank cleave (Groupe \xE0 l'ext\xE9rieur)",
            ja: "\u30BF\u30F3\u30AF\u982D\u5272\u308A (PT\u306F\u5916\u3078)",
            cn: "\u5766\u514B\u987A\u5288 (\u4EBA\u7FA4\u907F\u5F00)",
            ko: "\uAD11\uC5ED \uD0F1\uBC84 (\uBCF8\uB300 \uBC16\uC73C\uB85C)",
            tc: "\u5766\u514B\u9806\u5288 (\u4EBA\u7FA4\u907F\u958B)"
          }
        };
        if (pull.slipstreamCount === 1 || pull.slipstreamCount > 4)
          return;
        if (!pull.garudaAwoken && pull.slipstreamCount === 4)
          return { alarmText: voice.tankCleavePartyOut() };
        if (pull.garudaAwoken)
          return { alertText: voice.partyStack() };
        return { infoText: voice.tankCleave() };
      }
    },
    {
      id: "UWU Garuda Mistral Song Marker",
      type: "HeadMarker",
      netRegex: { id: "0010" },
      condition: Condition.targetIsYou(),
      alertText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Mistral on YOU",
          de: "Mistral-Song",
          fr: "Mistral sur VOUS",
          ja: "\u30DF\u30B9\u30C8\u30E9\u30EB\u30BD\u30F3\u30B0",
          cn: "\u5BD2\u98CE\u4E4B\u6B4C\u70B9\u540D",
          ko: "\uC0AD\uD48D \uC9D5",
          tc: "\u5BD2\u98A8\u4E4B\u6B4C\u9EDE\u540D"
        }
      }
    },
    {
      id: "UWU Garuda Mistral Song Tank",
      type: "HeadMarker",
      netRegex: { id: "0010", capture: false },
      condition: (pull) => pull.role === "tank",
      suppressSeconds: 5,
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Block Mistral Song",
          de: "Mistral-Song",
          fr: "Chant du mistral",
          ja: "\u30DF\u30B9\u30C8\u30E9\u30EB\u30BD\u30F3\u30B0",
          cn: "\u5BD2\u98CE\u4E4B\u6B4C",
          ko: "\uC0AD\uD48D \uC9D5",
          tc: "\u5BD2\u98A8\u4E4B\u6B4C"
        }
      }
    },
    {
      id: "UWU Garuda Spiny Plume",
      type: "AddedCombatant",
      netRegex: { name: "Spiny Plume", capture: false },
      condition: (pull) => pull.role === "tank",
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Spiny Plume Add",
          de: "Dorniger Federsturm",
          fr: "Add Plume perforante",
          ja: "\u30B9\u30D1\u30A4\u30CB\u30FC\u30D7\u30EB\u30FC\u30E0",
          cn: "\u523A\u7FBD\u51FA\u73B0",
          ko: "\uAC00\uC2DC\uB3CB\uD78C \uAE43\uD138 \uB4F1\uC7A5",
          tc: "\u523A\u7FBD\u51FA\u73FE"
        }
      }
    },
    {
      id: "UWU Garuda Wicked Wheel",
      type: "StartsUsing",
      netRegex: { id: "2B4E", source: "Garuda", capture: false },
      condition: (pull) => pull.phase === "garuda",
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          unawokenOut: Voices.out,
          awokenOutThenIn: Voices.outThenIn
        };
        if (pull.garudaAwoken)
          return { alertText: voice.awokenOutThenIn() };
        return { infoText: voice.unawokenOut() };
      }
    },
    {
      id: "UWU Garuda Aerial Blast",
      type: "StartsUsing",
      netRegex: { id: "2B55", source: "Garuda", capture: false },
      condition: (pull) => pull.phase === "garuda",
      response: Response.aoe()
    },
    {
      id: "UWU Garuda Mistral Shriek",
      type: "StartsUsing",
      netRegex: { id: "2B54", source: "Garuda", capture: false },
      response: Response.aoe()
    },
    {
      id: "UWU Garuda Sisters Location",
      comment: {
        en: "Where the two sisters are for the tanks to block. dir1 is always the first sister location starting North and going clockwise",
        de: "Wo sich die beiden Schwestern befinden, die die Tanks blockieren sollen. dir1 ist immer die erste Schwester, die im Norden beginnt und im Uhrzeigersinn verl\xE4uft.",
        fr: "L'emplacement des deux s\u0153urs \xE0 bloquer pour les tanks. dir1 est toujours le premier emplacement de la s\u0153ur en commen\xE7ant par le nord et en allant dans le sens des aiguilles d'une montre.",
        ja: "\u30BF\u30F3\u30AF\u304C\u30D6\u30ED\u30C3\u30AF\u3059\u308B2\u4EBA\u306E\u5206\u8EAB\u306E\u4F4D\u7F6E\u3002dir1 \u306F\u57FA\u672C\u7684\u306B\u300C\u5317\u300D\u304B\u3089\u59CB\u307E\u308A\u3001\u6642\u8A08\u56DE\u308A\u306B\u6700\u521D\u306E\u5206\u8EAB\u306E\u4F4D\u7F6E\u306B\u623B\u308A\u307E\u3059\u3002",
        cn: "\u4E24\u5206\u8EAB\u5F85\u5766\u514B\u963B\u6321\u7684\u4F4D\u7F6E\u3002dir1 \u59CB\u7EC8\u662F\u4ECE\u5730\u56FE\u4E0A\u65B9\u5F00\u59CB\u987A\u65F6\u9488\u65B9\u5411\u7684\u7B2C\u4E00\u4E2A\u5206\u8EAB\u4F4D\u7F6E",
        ko: "\uD0F1\uCEE4\uAC00 \uB9C9\uC744 \uB450 \uBD84\uC2E0\uC758 \uC704\uCE58. dir1\uC740 \uBD81\uCABD\uC5D0\uC11C \uC2DC\uACC4\uBC29\uD5A5\uC73C\uB85C \uB3C4\uB294 \uAC83\uC744 \uAE30\uC900\uC73C\uB85C \uD56D\uC0C1 \uCCAB \uBC88\uC9F8 \uBD84\uC2E0\uC758 \uC704\uCE58\uC785\uB2C8\uB2E4",
        tc: "\u5169\u5206\u8EAB\u5F85\u5766\u514B\u963B\u64CB\u7684\u4F4D\u7F6E\u3002dir1 \u59CB\u7D42\u662F\u5F9E\u5730\u5716\u4E0A\u65B9\u958B\u59CB\u9806\u6642\u91DD\u65B9\u5411\u7684\u7B2C\u4E00\u500B\u5206\u8EAB\u4F4D\u7F6E"
      },
      type: "StartsUsing",
      netRegex: { id: "2B55", source: "Garuda", capture: false },

      condition: (pull) => pull.phase === "garuda",
      delaySeconds: 19,
      promise: (pull) => {
        pull.combatantData = [];
        pull.combatantData = (sayOverlayHandler({
          call: "getCombatants"
        })).combatants;
      },
      alertText: (pull, _hit, voice) => {
        const sister = pull.combatantData.filter(
          (x) => x.BNpcNameID === 1645 || x.BNpcNameID === 1646
        );
        const [dir1s, dir2s] = sister.map(
          (c) => Facings.xyTo4DirNum(c.PosX, c.PosY, centerXBit, centerYBit)
        ).sort();
        if (dir1s === void 0 || dir2s === void 0 || sister.length !== 2)
          return;
        const table = {
          0: voice.dirN(),
          1: voice.dirE(),
          2: voice.dirS(),
          3: voice.dirW()
        };
        return voice.text({ dir1: table[dir1s], dir2: table[dir2s] });
      },
      outputStrings: {
        text: {
          en: "Sisters: ${dir1} / ${dir2}",
          de: "Schwestern: ${dir1} / ${dir2}",
          fr: "S\u0153urs : ${dir1} / ${dir2}",
          ja: "\u5206\u8EAB: ${dir1} / ${dir2}",
          cn: "\u5206\u8EAB\uFF1A${dir1} / ${dir2}",
          ko: "\uBD84\uC2E0: ${dir1} / ${dir2}",
          tc: "\u5206\u8EAB\uFF1A${dir1} / ${dir2}"
        },

        dirN: Voices.dirN,
        dirE: Voices.dirE,
        dirS: Voices.dirS,
        dirW: Voices.dirW
      }
    },
    {
      id: "UWU Ultima Mesohigh Tether",
      type: "Tether",

      netRegex: { id: "0004", capture: false },
      suppressSeconds: 30,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {

          garuda2: {
            en: "Get Sister Tether!!!",
            de: "Nimm Verbindung von der Schwester!!!",
            fr: "Prenez le lien de la s\u0153ur !!!",
            ja: "\u5206\u8EAB\u306E\u7DDA\u3092\u53D6\u3063\u3066!!!",
            cn: "\u63A5\u5206\u8EAB\u7684\u7EBF!!!",
            ko: "\uBD84\uC2E0 \uC904 \uAC00\uC838\uAC00\uAE30!!!",
            tc: "\u63A5\u5206\u8EAB\u7684\u7DDA!!!"
          },

          garuda1: {
            en: "Sister Tethers",
            de: "Schwester Verbindungen",
            fr: "Lien de la s\u0153ur",
            ja: "\u5206\u8EAB\u306E\u7DDA",
            cn: "\u5206\u8EAB\u8FDE\u7EBF",
            ko: "\uBD84\uC2E0 \uC904",
            tc: "\u5206\u8EAB\u9023\u7DDA"
          },

          annihilation1: {
            en: "Tether",
            de: "Verbindungen",
            fr: "Lien",
            ja: "\u7DDA",
            cn: "\u8FDE\u7EBF",
            ko: "\uC904",
            tc: "\u9023\u7DDA"
          },

          suppression1: {
            en: "Tether!!!",
            de: "Verbindungen!!!",
            fr: "Lien !!!",
            ja: "\u7DDA!!!",
            cn: "\u8FDE\u7EBF!!!",
            ko: "\uC904!!!",
            tc: "\u9023\u7DDA!!!"
          }
        };
        const myStack = pull.thermalLow[pull.me];
        if (myStack === void 0 || myStack === 0)
          return;
        if (myStack === 2) {
          if (pull.phase === "garuda" && !pull.garudaAwoken)
            return { alarmText: voice.garuda2() };
          return;
        }
        if (pull.phase === "garuda")
          return { alertText: voice.garuda1() };
        if (pull.phase === "annihilation")
          return { infoText: voice.annihilation1() };
        if (pull.phase === "suppression")
          return { alarmText: voice.suppression1() };
      }
    },

    {
      id: "UWU Ifrit Possible ID Locator",
      type: "StartsUsing",
      netRegex: { id: "2B55", source: "Garuda", capture: false },

      delaySeconds: 25,
      run: (pull) => {
        pull.possibleIfritIDs = pull.combatantData.filter((c) => c.BNpcNameID === 1185).map((c) => c.ID?.toString(16).toUpperCase() ?? "");
      }
    },
    {
      id: "UWU Ifrit Initial Dash Collector",
      type: "ActorSetPos",

      netRegex: { id: "4[0-9A-Fa-f]{7}", capture: true },
      condition: (pull, hit) => {
        if (!pull.possibleIfritIDs.includes(hit.id))
          return false;
        const placeXVal = parseFloat(hit.x ?? "0");
        const placeYVal = parseFloat(hit.y ?? "0");
        if (placeXVal === 0 || placeYVal === 0)
          return false;
        if (Math.abs(placeXVal - 100) - 19.5 < Number.EPSILON || Math.abs(placeYVal - 100) - 19.5 < Number.EPSILON)
          return true;
        return false;
      },
      suppressSeconds: 9999,
      infoText: (pull, hit, voice) => {
        const placeXVal = parseFloat(hit.x ?? "0");
        const placeYVal = parseFloat(hit.y ?? "0");
        let ifritFacing = "unknown";
        if (placeXVal < 95) {
          pull.ifritRadiantPlumeLocations.push("dirW", "dirE");
          ifritFacing = "dirW";
        } else if (placeXVal > 105) {
          pull.ifritRadiantPlumeLocations.push("dirW", "dirE");
          ifritFacing = "dirE";
        } else if (placeYVal < 95) {
          pull.ifritRadiantPlumeLocations.push("dirN", "dirS");
          ifritFacing = "dirN";
        } else if (placeYVal > 105) {
          pull.ifritRadiantPlumeLocations.push("dirN", "dirS");
          ifritFacing = "dirS";
        }
        pull.ifritRadiantPlumeLocations = pull.ifritRadiantPlumeLocations.filter((place, spotTwo) => pull.ifritRadiantPlumeLocations.indexOf(place) === spotTwo);
        return voice.text({ dir: voice[ifritFacing]() });
      },
      outputStrings: {
        text: {
          en: "Ifrit ${dir}",
          de: "Ifrit ${dir}",
          fr: "Ifrit ${dir}",
          ja: "\u30A4\u30D5\u30EA\u30FC\u30C8 ${dir}",
          cn: "\u706B\u795E ${dir}",
          ko: "\uC774\uD504\uB9AC\uD2B8 ${dir}",
          tc: "\u706B\u795E ${dir}"
        },
        unknown: Voices.unknown,
        ...Facings.outputStringsCardinalDir
      }
    },
    {
      id: "UWU Ifrit Initial Radiant Plume Collector",
      type: "StartsUsingExtra",
      netRegex: { id: "2B61" },
      condition: (pull, hit) => {
        const placeXVal = parseFloat(hit.x);
        const placeYVal = parseFloat(hit.y);
        if (Math.abs(placeXVal - 100) < 1) {
          if (Math.abs(placeYVal - 83) < 1) {
            pull.ifritRadiantPlumeLocations.push("dirN");
          } else if (Math.abs(placeYVal - 118) < 1) {
            pull.ifritRadiantPlumeLocations.push("dirS");
          }
        } else if (Math.abs(placeYVal - 100) < 1) {
          if (Math.abs(placeXVal - 83) < 1) {
            pull.ifritRadiantPlumeLocations.push("dirW");
          } else if (Math.abs(placeXVal - 118) < 1) {
            pull.ifritRadiantPlumeLocations.push("dirE");
          }
        }
        pull.ifritRadiantPlumeLocations = pull.ifritRadiantPlumeLocations.filter((place, spotTwo) => pull.ifritRadiantPlumeLocations.indexOf(place) === spotTwo);
        return pull.ifritRadiantPlumeLocations.length === 3;
      },
      suppressSeconds: 5,
      infoText: (pull, _hit, voice) => {
        if (pull.ifritRadiantPlumeLocations.length < 3)
          return;
        const clearFacing = Facings.outputCardinalDir.filter(
          (facingTwo) => !pull.ifritRadiantPlumeLocations.includes(facingTwo)
        )[0];
        return voice[clearFacing ?? "unknown"]();
      },
      outputStrings: {
        unknown: Voices.unknown,
        ...Facings.outputStringsCardinalDir
      }
    },
    {
      id: "UWU Ifrit Vulcan Burst",
      type: "StartsUsing",
      netRegex: { id: "25B7", source: "Ifrit", capture: false },
      response: Response.knockback()
    },
    {
      id: "UWU Ifrit Nail Adds",
      type: "AddedCombatant",
      netRegex: { npcNameId: "1186", npcBaseId: "8731" },
      condition: (pull, hit) => {
        pull.nailAdds.push(hit);
        return pull.nailAdds.length === 4;
      },
      alertText: (pull, _hit, voice) => {
        const facings = pull.nailAdds.map((m) => {
          return Facings.addedCombatantPosTo8Dir(m, centerXBit, centerYBit);
        }).sort();
        for (let i = 0; i < facings.length; ++i) {
          const this8Facing = facings[i];
          const next8Facing = facings[(i + 1) % facings.length];
          if (this8Facing === void 0 || next8Facing === void 0)
            break;
          if (next8Facing - this8Facing === 1 || this8Facing - next8Facing === 7) {
            const between16Facing = this8Facing * 2 + 1;
            const voiceTag = Facings.output16Dir[between16Facing] ?? "unknown";
            return voice.text({ dir: voice[voiceTag]() });
          }
        }
      },
      outputStrings: {
        text: {
          en: "Near: ${dir}",
          de: "Nahe: ${dir}",
          fr: "Proche : ${dir}",
          ja: "\u8FD1\u3044\u307B\u3046: ${dir}",
          cn: "\u8FD1: ${dir}",
          ko: "\uAC00\uAE4C\uC6B4 \uAE30\uB465: ${dir}",
          tc: "\u8FD1: ${dir}"
        },
        ...Facings.outputStrings16Dir
      }
    },
    {
      id: "UWU Ifrit Nail Deaths",
      type: "Ability",
      netRegex: { id: "2B58" },
      condition: (pull, hit) => {
        if (pull.nailDeaths[hit.sourceId] === void 0) {
          pull.nailDeaths[hit.sourceId] = hit;
          pull.nailDeathOrder.push(hit.sourceId);
        }
        return pull.nailDeathOrder.length === 4;
      },
      suppressSeconds: 999999,
      run: (pull) => {
        const codeToFacing = {};
        let priorFacing;
        let priorRotationFacing;
        for (const tagTwo of pull.nailDeathOrder) {
          const m = pull.nailDeaths[tagTwo];
          if (m === void 0)
            return;
          const x = parseFloat(m.x);
          const y = parseFloat(m.y);
          const this8Facing = Facings.xyTo8DirNum(x, y, centerXBit, centerYBit);
          codeToFacing[m.sourceId] = this8Facing;
          const thisFacing = this8Facing % 4;
          if (priorFacing === void 0) {
            priorFacing = thisFacing;
            continue;
          }
          const isCWBit = thisFacing - priorFacing === 1 || priorFacing - thisFacing === 3;
          const isCCWBit = priorFacing - thisFacing === 1 || thisFacing - priorFacing === 3;
          const thisRotationFacing = isCWBit ? "cw" : isCCWBit ? "ccw" : void 0;
          priorFacing = thisFacing;
          if (thisRotationFacing === void 0)
            return;
          if (priorRotationFacing === void 0) {
            priorRotationFacing = thisRotationFacing;
            continue;
          }
          if (thisRotationFacing !== priorRotationFacing)
            return;
        }
        const leadNailCode = pull.nailDeathOrder[0];
        const priorNailCode = pull.nailDeathOrder[3];
        pull.nailDeathRotationDir = priorRotationFacing;
        if (leadNailCode !== void 0)
          pull.nailDeathFirst8Dir = codeToFacing[leadNailCode];
        if (priorNailCode !== void 0)
          pull.nailDeathLast8Dir = codeToFacing[priorNailCode];
      }
    },
    {
      id: "UWU Ifrit Fetters",
      type: "Tether",

      netRegex: { id: "0009" },
      condition: (pull, hit) => hit.target === pull.me || hit.source === pull.me,
      infoText: (pull, hit, voice) => {
        const otherMember = hit.target === pull.me ? hit.source : hit.target;
        return voice.fetters({ player: pull.party.member(otherMember) });
      },
      outputStrings: {
        fetters: {
          en: "Fetters (w/${player})",
          de: "Fesseln (mit ${player})",
          fr: "Entraves (avec ${player})",
          ja: "\u9396 (\u76F8\u624B: ${player})",
          cn: "\u9501\u94FE (\u4E0E /${player})",
          ko: "\uC0AC\uC2AC (+${player})",
          tc: "\u9396\u93C8 (\u8207 ${player})"
        }
      }
    },
    {
      id: "UWU Ifrit Searing Wind",
      type: "StartsUsing",
      netRegex: { id: "2B5B", source: "Ifrit" },
      condition: Condition.targetIsYou(),
      alarmText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Searing Wind on YOU",
          de: "Versengen auf DIR",
          fr: "Carbonisation sur VOUS",
          ja: "\u81EA\u5206\u306B\u707C\u71B1",
          cn: "\u707C\u70ED\u5486\u54EE\u70B9\u540D",
          ko: "\uC791\uC5F4 \uB300\uC0C1\uC790",
          tc: "\u707C\u71B1\u5486\u54EE\u9EDE\u540D"
        }
      }
    },
    {
      id: "UWU Ifrit Hellfire",
      type: "StartsUsing",
      netRegex: { id: "2B5E", source: "Ifrit", capture: false },
      condition: (pull) => pull.phase === "ifrit",
      response: Response.aoe()
    },
    {
      id: "UWU Ifrit Incinerate",
      type: "StartsUsing",
      netRegex: { id: "2B56", source: "Ifrit", capture: false },
      suppressSeconds: 5,
      response: Response.tankCleave()
    },
    {
      id: "UWU Ifrit Name Toggle Counter",
      type: "NameToggle",
      netRegex: { name: "Ifrit", toggle: "00", capture: false },
      run: (pull) => pull.ifritUntargetableCount++
    },
    {
      id: "UWU Ifrit Dash Safe Spot 1",

      comment: {
        en: `If the first nail is SE, this will call SE/NW for both reverse-Z and normal-Z.
             If the first nail is S, this will call SE/NW for reverse-Z and SW/NE for normal-Z.
             Other nail orders are also supported, these are just examples.`,
        de: `Wenn der erste Nagel SO ist, wird dies SO/NW sowohl f\xFCr Umgekehrtes-Z als auch f\xFCr Normal-Z aufgerufen.
             Wenn der erste Nagel S ist, wird dies SO/NW f\xFCr Umgekehrtes-Z und SW/NO f\xFCr Normal-Z aufgerufen.
             Andere Nagelreihenfolgen werden ebenfalls unterst\xFCtzt, dies sind nur Beispiele.`,
        fr: `Si le premier clou est SE, on annoncera SE/NO pour les Z invers\xE9s et les Z normaux.
             Si le premier clou est S, on annoncera SE/NO pour la zone invers\xE9e et SW/NO pour la zone normale.
             D'autres ordres de clous sont \xE9galement possibles, il ne s'agit que d'exemples.`,
        ja: `\u6700\u521D\u306E\u6954\u304C\u5357\u6771\u306E\u5834\u5408\u3001\u9006Z\u3068\u901A\u5E38Z\u306E\u4E21\u65B9\u3067\u5357\u6771/\u5317\u897F\u306B\u51FA\u73FE\u3057\u307E\u3059\u3002
             \u6700\u521D\u306E\u6954\u304C\u5357\u306E\u5834\u5408\u3001\u9006Z\u306A\u3089\u5357\u6771/\u5317\u897F\u3001\u901A\u5E38Z\u306A\u3089\u5357\u897F/\u5317\u6771\u306B\u51FA\u73FE\u3057\u307E\u3059\u3002
             \u3053\u308C\u306F\u4E00\u4F8B\u3067\u3001\u4ED6\u306E\u91D8\u306E\u9806\u5E8F\u3082\u30B5\u30DD\u30FC\u30C8\u3055\u308C\u3066\u3044\u307E\u3059\u3002`,
        cn: `\u5982\u679C\u7B2C\u4E00\u4E2A\u706B\u795E\u67F1\u5728\u53F3\u4E0B\uFF0C\u5219\u53CD\u5411 Z \u548C\u6B63\u5E38 Z \u90FD\u4F1A\u63D0\u793A\u53F3\u4E0B/\u5DE6\u4E0A
             \u5982\u679C\u7B2C\u4E00\u4E2A\u706B\u795E\u67F1\u5728\u4E0B, \u5219\u53CD\u5411 Z \u5C06\u63D0\u793A\u53F3\u4E0B/\u5DE6\u4E0A\uFF0C\u6B63\u5E38 Z \u5C06\u63D0\u793A\u5DE6\u4E0B/\u53F3\u4E0A\u3002
             \u8FD9\u4E9B\u53EA\u662F\u4E3E\u4F8B, \u5176\u4ED6\u706B\u795E\u67F1\u987A\u5E8F\u4E5F\u652F\u6301\u3002`,
        ko: `\uCCAB \uBC88\uC9F8 \uAE30\uB465\uC774 \uB0A8\uB3D9\uCABD\uC778 \uACBD\uC6B0, \uC5ED\uBC29\uD5A5 Z\uC640 \uC77C\uBC18 Z \uBAA8\uB450\uC5D0 \uB300\uD574 \uB0A8\uB3D9/\uBD81\uC11C\uB97C \uD638\uCD9C\uD569\uB2C8\uB2E4.
             \uCCAB \uBC88\uC9F8 \uAE30\uB465\uC774 \uB0A8\uCABD\uC778 \uACBD\uC6B0, \uC5ED\uBC29\uD5A5 Z\uB294 \uB0A8\uB3D9/\uBD81\uC11C\uB97C, \uC77C\uBC18 Z\uB294 \uB0A8\uC11C/\uBD81\uB3D9\uB97C \uD638\uCD9C\uD569\uB2C8\uB2E4.
             \uB2E4\uB978 \uAE30\uB465 \uC21C\uC11C\uB3C4 \uC9C0\uC6D0\uB418\uBA70, \uC774\uB294 \uC608\uC2DC\uC77C \uBFD0\uC785\uB2C8\uB2E4.`,
        tc: `\u5982\u679C\u7B2C\u4E00\u500B\u706B\u795E\u67F1\u5728\u6771\u5357\uFF0C\u5247\u53CD\u5411 Z \u548C\u6B63\u5E38 Z \u90FD\u6703\u63D0\u793A\u6771\u5357/\u897F\u5317
             \u5982\u679C\u7B2C\u4E00\u500B\u706B\u795E\u67F1\u5728\u5357\u9762, \u5247\u53CD\u5411 Z \u5C07\u63D0\u793A\u6771\u5357/\u897F\u5317\uFF0C\u6B63\u5E38 Z \u5C07\u63D0\u793A\u897F\u5357/\u6771\u5317\u3002
             \u9019\u4E9B\u53EA\u662F\u8209\u4F8B, \u5176\u4ED6\u706B\u795E\u67F1\u9806\u5E8F\u4E5F\u652F\u63F4\u3002`
      },
      type: "NameToggle",
      netRegex: { name: "Ifrit", toggle: "00", capture: false },
      condition: (pull) => pull.ifritUntargetableCount === 1,
      durationSeconds: 5,
      alertText: (pull, _hit, voice) => {
        const leadNailFacing = pull.nailDeathFirst8Dir;
        const rotationTyp = pull.nailDeathRotationDir;
        if (leadNailFacing === void 0 || rotationTyp === void 0)
          return;
        const oppositeRotations = rotationTyp === "cw" ? 7 : 1;
        const isIntercards = leadNailFacing % 2 === 1;
        const dir1s = isIntercards ? leadNailFacing : (leadNailFacing + oppositeRotations) % 8;
        const dir2s = (dir1s + 4) % 8;
        const dir1Word = voice[Facings.outputFrom8DirNum(dir1s)]();
        const dir2Word = voice[Facings.outputFrom8DirNum(dir2s)]();
        return voice.intercardSafeSpot({ dir1: dir1Word, dir2: dir2Word });
      },
      outputStrings: {
        intercardSafeSpot: {
          en: "${dir1} / ${dir2}",
          de: "${dir1} / ${dir2}",
          fr: "${dir1} / ${dir2}",
          ja: "${dir1} / ${dir2}",
          cn: "${dir1} / ${dir2}",
          ko: "${dir1} / ${dir2}",
          tc: "${dir1} / ${dir2}"
        },
        ...Facings.outputStrings8Dir
      }
    },
    {
      id: "UWU Ifrit Dash Safe Spot 2 Adjust",
      comment: {
        en: `If the first nail was on an intercard, then the first Ifrit dash is on an intercard
             and this optional call is to move to be adjacent to that first dash.
             If you are already safe, this will not be called.`,
        de: `Wenn der erste Nagel Interkardinal war, dann ist der erste Ifrit-Ansturm auf einer Interkardinalen
             und dieser optionale Aufruf besteht darin, sich in die N\xE4he dieses ersten Ansturms zu bewegen.
             Wenn man bereits in Sicherheit ist, wird dies nicht aufgerufen.`,
        fr: `Si le premier clou \xE9tait en intercardinal, alors le premier dash d'Ifrit est en intercardinal
             et cette annonce optionnelle vous pr\xE9viens de vous d\xE9placer pour \xEAtre adjacent \xE0 ce premier dash.
             Si vous \xEAtes d\xE9j\xE0 en s\xE9curit\xE9, cette option n'est pas activ\xE9e.`,
        ja: `\u6700\u521D\u306E\u6954\u304C\u5317\u6771\u3001\u5357\u6771\u3001\u5357\u897F\u3001\u5317\u897F\u306A\u3089\u3001\u6700\u521D\u306E\u30A4\u30D5\u30EA\u30FC\u30C8\u306E\u7A81\u9032\u3082\u5317\u6771\u3001\u5357\u6771\u3001\u5357\u897F\u3001\u5317\u897F\u306B\u306A\u308A\u3001
             \u3053\u306E\u30AA\u30D7\u30B7\u30E7\u30F3\u306F\u305D\u306E\u6700\u521D\u306E\u30C0\u30C3\u30B7\u30E5\u306B\u96A3\u63A5\u3059\u308B\u3088\u3046\u306B\u79FB\u52D5\u3059\u308B\u305F\u3081\u306E\u3082\u306E\u3067\u3059\u3002
             \u3059\u3067\u306B\u5B89\u5730\u306B\u3044\u308B\u5834\u5408\u3001\u3053\u308C\u306F\u547C\u3073\u51FA\u3055\u308C\u307E\u305B\u3093\u3002`,
        cn: `\u5982\u679C\u7B2C\u4E00\u4E2A\u706B\u795E\u67F1\u5728\u5BF9\u89D2\u7EBF\u4E0A\uFF0C\u90A3\u4E48\u7B2C\u4E00\u6B21\u706B\u795E\u51B2\u4E5F\u5728\u5BF9\u89D2\u7EBF\u4E0A\u3002
             \u8FD9\u4E2A\u53EF\u9009\u63D0\u793A\u4F1A\u63D0\u793A\u4F60\u79FB\u52A8\u5230\u7B2C\u4E00\u6B21\u706B\u795E\u51B2\u9644\u8FD1\u7684\u4F4D\u7F6E\u3002
             \u5982\u679C\u4F60\u5DF2\u5728\u5B89\u5168\u533A\uFF0C\u5219\u4E0D\u4F1A\u8F93\u51FA\u6B64\u63D0\u793A\u3002`,
        ko: `\uCCAB \uBC88\uC9F8 \uAE30\uB465\uC774 \uB300\uAC01\uC120\uC5D0 \uC788\uC73C\uBA74 \uCCAB \uBC88\uC9F8 \uC774\uD504\uB9AC\uD2B8 \uB3CC\uC9C4\uB3C4 \uB300\uAC01\uC120\uC5D0 \uC788\uC73C\uBA70,
             \uC774 \uC54C\uB78C\uC740 \uCCAB \uBC88\uC9F8 \uB3CC\uC9C4 \uC606\uC73C\uB85C \uC774\uB3D9\uD558\uB77C\uB294 \uAC83\uC774 \uB429\uB2C8\uB2E4.
             \uC774\uBBF8 \uC548\uC804\uD558\uB2E4\uBA74 \uC774 \uC54C\uB78C\uC740 \uD638\uCD9C\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.`,
        tc: `\u5982\u679C\u7B2C\u4E00\u500B\u706B\u795E\u67F1\u5728\u5C0D\u89D2\u7DDA\u4E0A\uFF0C\u90A3\u9EBC\u7B2C\u4E00\u6B21\u706B\u795E\u885D\u4E5F\u5728\u5C0D\u89D2\u7DDA\u4E0A\u3002
             \u9019\u500B\u53EF\u9078\u63D0\u793A\u6703\u63D0\u793A\u4F60\u79FB\u52D5\u5230\u7B2C\u4E00\u6B21\u706B\u795E\u885D\u9644\u8FD1\u7684\u4F4D\u7F6E\u3002
             \u5982\u679C\u4F60\u5DF2\u5728\u5B89\u5168\u5340\uFF0C\u5247\u4E0D\u6703\u8F38\u51FA\u6B64\u63D0\u793A\u3002`
      },
      type: "NameToggle",
      netRegex: { name: "Ifrit", toggle: "00", capture: false },

      condition: (pull) => pull.ifritUntargetableCount === 2 && pull.ifritAwoken,
      infoText: (pull, _hit, voice) => {
        const leadNailFacing = pull.nailDeathFirst8Dir;
        const rotationTyp = pull.nailDeathRotationDir;
        if (leadNailFacing === void 0 || rotationTyp === void 0)
          return;
        const isIntercards = leadNailFacing % 2 === 1;
        if (!isIntercards)
          return;
        const facingWord = rotationTyp === "cw" ? voice.counterclockwise() : voice.clockwise();
        return voice.text({ rotation: facingWord });
      },
      outputStrings: {
        text: {
          en: "Adjust 45\xB0 ${rotation}",
          de: "Rotiere 45\xB0 ${rotation}",
          fr: "Ajustez de 45\xB0 ${rotation}",
          ja: "45\xB0 ${rotation} \u306B\u8ABF\u6574",
          cn: "${rotation} \u65CB\u8F6C 45\xB0",
          ko: "${rotation} 45\xB0 \uC774\uB3D9",
          tc: "${rotation} \u65CB\u8F49 45\xB0"
        },
        clockwise: Voices.clockwise,
        counterclockwise: Voices.counterclockwise
      }
    },
    {
      id: "UWU Ifrit Dash Safe Spot 2",
      comment: {
        en: `This is the major movement for the Ifrit dashes starting adjacent to the first dash.
             Both the party and the healer will move either 45 or 90 degrees.
             It is a "fast" movement if you need to move fast to avoid the Ifrit follow-up dash.
             It is a "slow" movement if you have extra time to do this.`,
        de: `Dies ist die Hauptbewegung f\xFCr die Ifrit-Anst\xFCrme, die in der N\xE4he des ersten Ansturms beginnt.
             Sowohl die Gruppe als auch der Heiler bewegen sich entweder um 45 oder 90 Grad.
             Es ist eine "schnelle" Bewegung, wenn man sich schnell bewegen muss, um dem Ifrit-Folgeschlag auszuweichen.
             Es ist eine "langsame" Bewegung, wenn man mehr Zeit hat, dies zu tun.`,
        fr: `Il s'agit du mouvement principal pour les dashs d'Ifrit qui commencent \xE0 c\xF4t\xE9 du premier dash.
             Le groupe et le soigneur se d\xE9placent de 45 ou 90 degr\xE9s.
             Il s'agit d'un mouvement "rapide" si vous devez vous d\xE9placer rapidement pour \xE9viter le dash suivant d'Ifrit.
             Il s'agit d'un mouvement "lent" si vous avez plus de temps pour le faire.`,
        ja: `\u3053\u308C\u306F\u3001\u6700\u521D\u306E\u7A81\u9032\u304B\u3089\u9023\u7D9A\u3059\u308B\u30A4\u30D5\u30EA\u30FC\u30C8\u7A81\u9032\u306E\u79FB\u52D5\u65B9\u6CD5\u3067\u3059\u3002
             \u30D1\u30FC\u30C6\u30A3\u5168\u4F53\u3068\u30D2\u30FC\u30E9\u30FC\u306F\u300145\u5EA6\u304B90\u5EA6\u3092\u5224\u65AD\u3057\u3066\u79FB\u52D5\u3057\u307E\u3059\u3002
             \u9023\u7D9A\u7A81\u9032\u3092\u907F\u3051\u308B\u305F\u3081\u306B\u3001\u3059\u3050\u79FB\u52D5\u3059\u308B\u5FC5\u8981\u304C\u3042\u308B\u5834\u5408\u306F\u300C\u6025\u300D\u3067\u3059\u3002
             \u4F59\u88D5\u304C\u3042\u308B\u5834\u5408\u306F\u300C\u9045\u300D\u3068\u306A\u308A\u307E\u3059\u3002`,
        cn: `\u8FD9\u662F\u4ECE\u7B2C\u4E00\u6B21\u706B\u795E\u51B2\u9644\u8FD1\u5F00\u59CB\u7684\u706B\u795E\u51B2\u4E3B\u8981\u79FB\u52A8\u3002
             \u4EBA\u7FA4\u548C\u5976\u5988\u90FD\u5C06\u79FB\u52A8 45 \u5EA6\u6216 90 \u5EA6\u3002
             "\u5FEB" \u8868\u793A\u9700\u8981\u5FEB\u901F\u79FB\u52A8\u624D\u80FD\u8EB2\u5F00\u706B\u795E\u51B2\u3002
             "\u6162" \u8868\u793A\u79FB\u52A8\u65F6\u95F4\u76F8\u5BF9\u6BD4\u8F83\u5145\u8DB3\u3002`,
        ko: `\uCCAB \uBC88\uC9F8 \uB3CC\uC9C4 \uC9C1\uD6C4\uBD80\uD130 \uC2DC\uC791\uB418\uB294 \uC774\uD504\uB9AC\uD2B8 \uB3CC\uC9C4\uC758 \uC8FC\uC694 \uB3D9\uC120\uC785\uB2C8\uB2E4.
             \uBCF8\uB300\uC640 \uD790\uB7EC \uBAA8\uB450 45\uB3C4 \uB610\uB294 90\uB3C4\uB85C \uC6C0\uC9C1\uC785\uB2C8\uB2E4.
             \uC774\uD504\uB9AC\uD2B8\uC758 \uD6C4\uC18D \uB3CC\uC9C4\uC744 \uD53C\uD558\uAE30 \uC704\uD574 \uBE60\uB974\uAC8C \uC774\uB3D9\uD574\uC57C \uD558\uB294 \uACBD\uC6B0 "\uBE60\uB978" \uC774\uB3D9\uC785\uB2C8\uB2E4.
             \uC2DC\uAC04\uC801 \uC5EC\uC720\uAC00 \uC788\uB2E4\uBA74 "\uB290\uB9B0" \uC774\uB3D9\uC785\uB2C8\uB2E4.`,
        tc: `\u9019\u662F\u5F9E\u7B2C\u4E00\u6B21\u706B\u795E\u885D\u9644\u8FD1\u958B\u59CB\u7684\u706B\u795E\u885D\u4E3B\u8981\u79FB\u52D5\u3002
             \u4EBA\u7FA4\u548C\u5976\u5ABD\u90FD\u5C07\u79FB\u52D5 45 \u5EA6\u6216 90 \u5EA6\u3002
             "\u5FEB" \u8868\u793A\u9700\u8981\u5FEB\u901F\u79FB\u52D5\u624D\u80FD\u8EB2\u958B\u706B\u795E\u885D\u3002
             "\u6162" \u8868\u793A\u79FB\u52D5\u6642\u9593\u76F8\u5C0D\u6BD4\u8F03\u5145\u8DB3\u3002`
      },
      type: "NameToggle",
      netRegex: { name: "Ifrit", toggle: "00", capture: false },
      condition: (pull) => pull.ifritUntargetableCount === 2 && pull.ifritAwoken,

      delaySeconds: 2.5,
      promise: (pull) => {
        pull.combatantData = [];
        if (pull.bossId.ifrit === void 0)
          return;
        pull.combatantData = (sayOverlayHandler({
          call: "getCombatants",
          ids: [parseInt(pull.bossId.ifrit, 16)]
        })).combatants;
      },
      alertText: (pull, _hit, voice) => {
        if (pull.phase === "titan")
          return;
        const [bodyTwo] = pull.combatantData;
        if (bodyTwo === void 0 || pull.combatantData.length !== 1)
          return;
        const leadNailFacing = pull.nailDeathFirst8Dir;
        const rotationTyp = pull.nailDeathRotationDir;
        if (leadNailFacing === void 0 || rotationTyp === void 0)
          return;
        const oppositeRotationFacing = rotationTyp === "cw" ? -1 : 1;
        const rotationFacing = rotationTyp === "cw" ? 1 : -1;
        const openFacing = (leadNailFacing + oppositeRotationFacing + 8) % 8;
        const ifritFacing = Facings.combatantStatePosTo8Dir(bodyTwo, centerXBit, centerYBit);
        for (let i = 1; i <= 4; ++i) {
          const dashFacing = (openFacing + i * rotationFacing + 8) % 8;
          if (dashFacing % 4 !== ifritFacing % 4)
            continue;
          const finalRotations = i === 1 || i === 3 ? rotationFacing : rotationFacing * 2;
          const finalFacing = (openFacing + finalRotations + 8) % 8;
          const finalFacingWord = voice[Facings.outputFrom8DirNum(finalFacing)]();
          const rotation = rotationTyp === "cw" ? voice.clockwise() : voice.counterclockwise();
          if (i === 1)
            return voice.awokenDash1({ rotation, dir: finalFacingWord });
          if (i === 2)
            return voice.awokenDash2({ rotation, dir: finalFacingWord });
          if (i === 3)
            return voice.awokenDash3({ rotation, dir: finalFacingWord });
          if (i === 4)
            return voice.awokenDash4({ rotation, dir: finalFacingWord });
        }
      },
      outputStrings: {
        awokenDash1: {
          en: "${rotation} 45\xB0 to ${dir} (fast)",
          de: "${rotation} 45\xB0 nach ${dir} (schnell)",
          fr: "${rotation} 45\xB0 vers ${dir} (rapide)",
          ja: "${rotation} 45\xB0 ${dir} \u306B (\u6025)",
          cn: "${rotation} 45\xB0 \u5230 ${dir} (\u5FEB)",
          ko: "${rotation} 45\xB0 ${dir}\uAE4C\uC9C0 (\uBE60\uB984)",
          tc: "${rotation} 45\xB0 \u5230 ${dir} (\u5FEB)"
        },
        awokenDash2: {
          en: "${rotation} 90\xB0 to ${dir} (fast)",
          de: "${rotation} 90\xB0 nach ${dir} (schnell)",
          fr: "${rotation} 90\xB0 vers ${dir} (rapide)",
          ja: "${rotation} 90\xB0 ${dir} \u306B (\u6025)",
          cn: "${rotation} 90\xB0 \u5230 ${dir} (\u5FEB)",
          ko: "${rotation} 90\xB0 ${dir}\uAE4C\uC9C0 (\uBE60\uB984)",
          tc: "${rotation} 90\xB0 \u5230 ${dir} (\u5FEB)"
        },
        awokenDash3: {
          en: "${rotation} 45\xB0 to ${dir} (slow)",
          de: "${rotation} 45\xB0 nach ${dir} (langsam)",
          fr: "${rotation} 45\xB0 vers ${dir} (lent)",
          ja: "${rotation} 45\xB0 ${dir} \u306B (\u9045)",
          cn: "${rotation} 45\xB0 \u5230 ${dir} (\u6162)",
          ko: "${rotation} 45\xB0 ${dir}\uAE4C\uC9C0 (\uB290\uB9BC)",
          tc: "${rotation} 45\xB0 \u5230 ${dir} (\u6162)"
        },
        awokenDash4: {
          en: "${rotation} 90\xB0 to ${dir} (slow)",
          de: "${rotation} 90\xB0 nach ${dir} (langsam)",
          fr: "${rotation} 90\xB0 vers ${dir} (lent)",
          ja: "${rotation} 90\xB0 ${dir} \u306B (\u9045)",
          cn: "${rotation} 90\xB0 \u5230 ${dir} (\u6162)",
          ko: "${rotation} 90\xB0 ${dir}\uAE4C\uC9C0 (\uB290\uB9BC)",
          tc: "${rotation} 90\xB0 \u5230 ${dir} (\u6162)"
        },
        clockwise: Voices.clockwise,
        counterclockwise: Voices.counterclockwise,
        ...Facings.outputStrings8Dir
      }
    },
    {
      id: "UWU Ifrit Flaming Crush",
      type: "HeadMarker",
      netRegex: { id: "0075", capture: false },
      alertText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Stack",
          de: "Stack",
          fr: "Packez-vous",
          ja: "\u982D\u5272\u308A",
          cn: "\u96C6\u5408\u5206\u644A",
          ko: "\uC9D1\uD569",
          tc: "\u96C6\u5408\u5206\u6524"
        }
      }
    },

    {
      id: "UWU Titan Last Move Collector",
      type: "ActorMove",
      netRegex: { id: "4[0-9a-fA-F]{7}", capture: true },
      condition: (pull, hit) => pull.phase === "titan" && pull.bossId.titan === hit.id,
      run: (pull, hit) => {
        pull.lastTitanMove = hit;
      }
    },
    {
      id: "UWU Titan Jump Direction",
      type: "NameToggle",
      netRegex: { id: "4[0-9a-fA-F]{7}", toggle: "00", capture: true },
      condition: (pull, hit) => pull.phase === "titan" && pull.bossId.titan === hit.id && pull.lastTitanMove !== void 0,

      delaySeconds: 0.4,
      alertText: (pull, _hit, voice) => {
        const unknownWord = voice.safe({ dir: voice.unknown() });
        const priorMove = pull.lastTitanMove;
        if (priorMove === void 0)
          return pull.seenTitanFirstJump ? unknownWord : voice.safe({ dir: voice["dirS"]() });
        const titanXBit = parseFloat(priorMove.x);
        const titanYBit = parseFloat(priorMove.y);
        const titanHdgs = parseFloat(priorMove.heading);
        for (const sit of jumpSite) {
          const hdgToTitans = readRelativeHdg(titanXBit, titanYBit, sit.x, sit.y);
          const raies = Math.abs(titanHdgs - hdgToTitans);
          if (raies >= 3 && raies <= 3.28)
            return voice.safe({ dir: voice[sit.safe]() });
        }
        return unknownWord;
      },
      run: (pull) => pull.seenTitanFirstJump = true,
      outputStrings: {
        safe: {
          en: "Safe: ${dir}",
          de: "Sicher: ${dir}",
          fr: "Sur : ${dir}",
          ja: "\u5B89\u5730: ${dir}",
          cn: "\u5B89\u5168\u533A: ${dir}",
          ko: "\uC548\uC804: ${dir}",
          tc: "\u5B89\u5168\u5340: ${dir}"
        },
        unknown: Voices.unknown,
        ...Facings.outputStringsCardinalDir
      }
    },
    {
      id: "UWU Titan Bury Direction",
      type: "AddedCombatant",
      netRegex: { npcNameId: "1803" },
      condition: (pull, hit) => {
        (pull.titanBury ??= []).push(hit);
        return pull.titanBury.length === 5;
      },
      alertText: (pull, _hit, voice) => {
        const bombTwo = (pull.titanBury ?? []).map((hit) => {
          return { x: parseFloat(hit.x), y: parseFloat(hit.y) };
        });
        if (bombTwo.length !== 5) {
          consol.error(`Titan Bury: wrong bombs size: ${JSON.stringify(pull.titanBury)}`);
          return;
        }
        const digitFacing = [0, 0, 0, 0];
        for (const bombsTwo of bombTwo) {
          if (bombsTwo.y < centerYBit)
            digitFacing[0]++;
          else
            digitFacing[2]++;
          if (bombsTwo.x < centerXBit)
            digitFacing[3]++;
          else
            digitFacing[1]++;
        }
        for (let spot = 0; spot < digitFacing.length; ++spot) {
          if (digitFacing[spot] !== 5)
            continue;
          const digitPort = digitFacing[(spot + 1) % 4] ?? -1;
          const digitStarboard = digitFacing[(spot - 1 + 4) % 4] ?? -1;
          if (digitStarboard === 2 && digitPort === 3)
            return voice.right();
          if (digitStarboard === 3 && digitPort === 2)
            return voice.left();
          consol.error(
            `Titan Bury: bad counts: ${JSON.stringify(pull.titanBury)}, ${spot}, ${digitPort}, ${digitStarboard}`
          );
          return;
        }
        consol.error(`Titan Bury: failed to find dir: ${JSON.stringify(pull.titanBury)}`);
      },
      outputStrings: {
        left: Voices.left,
        right: Voices.right
      }
    },
    {
      id: "UWU Titan Gaols",
      type: "Ability",
      netRegex: { id: ["2B6C", "2B6B"], source: ["Garuda", "Titan"] },
      condition: (pull) => !pull.seenTitanGaols,
      preRun: (pull, hit) => {
        pull.titanGaols.push(hit.target);
        if (pull.titanGaols.length !== 3)
          return;
        const rawCageSequence = [
          pull.triggerSetConfig.gaolOrder1,
          pull.triggerSetConfig.gaolOrder2,
          pull.triggerSetConfig.gaolOrder3,
          pull.triggerSetConfig.gaolOrder4,
          pull.triggerSetConfig.gaolOrder5,
          pull.triggerSetConfig.gaolOrder6,
          pull.triggerSetConfig.gaolOrder7,
          pull.triggerSetConfig.gaolOrder8,
          pull.triggerSetConfig.gaolOrder9,
          pull.triggerSetConfig.gaolOrder10,
          pull.triggerSetConfig.gaolOrder11,
          pull.triggerSetConfig.gaolOrder12,
          pull.triggerSetConfig.gaolOrder13,
          pull.triggerSetConfig.gaolOrder14,
          pull.triggerSetConfig.gaolOrder15,
          pull.triggerSetConfig.gaolOrder16,
          pull.triggerSetConfig.gaolOrder17,
          pull.triggerSetConfig.gaolOrder18,
          pull.triggerSetConfig.gaolOrder19,
          pull.triggerSetConfig.gaolOrder20
        ].map((x) => x.trim()).filter((x) => x !== "");
        const crewLabels = [...pull.party.partyNames].sort((a, b) => a.localeCompare(b));
        const cageSequence = [];
        for (const entriesTwo of rawCageSequence) {
          if (entriesTwo.length !== 3) {
            cageSequence.push(entriesTwo.toLocaleLowerCase());
            continue;
          }
          const uppercaseCraftEntry = entriesTwo.toUpperCase();
          for (const labelTwo of crewLabels) {
            const craftWord = pull.party.jobName(labelTwo);
            if (craftWord === uppercaseCraftEntry)
              cageSequence.push(labelTwo.toLocaleLowerCase());
          }
        }
        pull.titanGaols.sort((a, b) => {
          const aSpot = cageSequence.indexOf(a.toLocaleLowerCase());
          const bSpot = cageSequence.indexOf(b.toLocaleLowerCase());
          if (aSpot === -1 && bSpot !== -1)
            return 1;
          if (bSpot === -1 && aSpot !== -1)
            return -1;
          if (aSpot < bSpot)
            return -1;
          if (bSpot < aSpot)
            return 1;
          return a.localeCompare(b);
        });
        if (pull.options.Debug) {
          consol.log(`GAOL CONFIG: ${JSON.stringify(rawCageSequence)}`);
          consol.log(`GAOL CONFIG NAME ORDER: ${JSON.stringify(cageSequence)}`);
          consol.log(`GAOL FINAL ORDER: ${JSON.stringify(pull.titanGaols)}`);
        }
      },
      alertText: (pull, _hit, voice) => {
        if (pull.titanGaols.length !== 3)
          return;
        const spot = pull.titanGaols.indexOf(pull.me);
        if (spot < 0)
          return;
        return voice[`num${spot + 1}`]();
      },
      infoText: (pull, _hit, voice) => {
        if (pull.titanGaols.length !== 3)
          return;
        return voice.text({
          player1: pull.party.member(pull.titanGaols[0]),
          player2: pull.party.member(pull.titanGaols[1]),
          player3: pull.party.member(pull.titanGaols[2])
        });
      },
      outputStrings: {
        num1: Voices.num1,
        num2: Voices.num2,
        num3: Voices.num3,
        text: {
          en: "${player1}, ${player2}, ${player3}",
          de: "${player1}, ${player2}, ${player3}",
          fr: "${player1}, ${player2}, ${player3}",
          ja: "${player1}, ${player2}, ${player3}",
          cn: "${player1}, ${player2}, ${player3}",
          ko: "${player1}, ${player2}, ${player3}",
          tc: "${player1}, ${player2}, ${player3}"
        }
      }
    },
    {

      id: "UWU Titan Bomb Failure",
      type: "Ability",
      netRegex: { id: "2B6A", source: "Bomb Boulder" },
      condition: (pull) => !pull.seenTitanGaols,
      alarmText: (pull, hit, voice) => {
        const spot = pull.titanGaols.indexOf(hit.target);
        if (spot === -1)
          return;
        const digitWord = voice[`num${spot + 1}`]();
        return voice.text({ num: digitWord, player: pull.party.member(hit.target) });
      },
      outputStrings: {

        num1: Voices.num1,
        num2: Voices.num2,
        num3: Voices.num3,
        text: {
          en: "Everyone to ${num} (${player} died)",
          de: "Alle zur ${num} (${player} ist gestorben)",
          fr: "Tout le monde sur ${num} (${player} est mort)",
          ja: "${num} \u3067 (${player} \u304C\u6B7B\u4EA1)",
          cn: "\u6240\u6709\u4EBA\u5230 ${num} (${player}\u6B7B\u4EA1)",
          ko: "\uC804\uBD80\uB2E4 ${num} \uCABD\uC73C\uB85C (${player} \uC8FD\uC74C)",
          tc: "\u6240\u6709\u4EBA\u5230 ${num} (${player} \u6B7B\u4EA1)"
        }
      }
    },
    {
      id: "UWU Titan Gaol Granite Impact",
      type: "StartsUsing",
      netRegex: { id: "2B6D", capture: false },

      run: (pull) => pull.seenTitanGaols = true
    },
    {
      id: "UWU Titan Rock Buster",
      type: "StartsUsing",
      netRegex: { id: "2B62", source: "Titan", capture: false },
      response: Response.tankBuster()
    },
    {
      id: "UWU Titan Mountain Buster",
      type: "StartsUsing",
      netRegex: { id: "2B63", source: "Titan", capture: false },
      response: Response.tankCleave()
    },
    {
      id: "UWU Titan Weight of the Land",
      type: "StartsUsing",
      netRegex: { id: "2B65", source: "Titan", capture: false },
      suppressSeconds: 3,
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: { text: { en: "Weight (dodge puddles)" } }
    },
    {
      id: "UWU Titan Geocrush",
      type: "StartsUsing",
      netRegex: { id: "2B66", source: "Titan", capture: false },
      response: Response.getOut()
    },
    {
      id: "UWU Titan Upheaval",
      type: "StartsUsing",
      netRegex: { id: "2B67", source: "Titan", capture: false },
      response: Response.knockback()
    },

    {
      id: "UWU Caster LB",
      type: "AddedCombatant",

      netRegex: { npcNameId: "2137", capture: false },
      condition: (pull) => Tool.isCasterDpsJob(pull.job) && pull.beyondLimits.has(pull.me) && pull.phase === "intermission",
      suppressSeconds: 5,
      alertText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Caster LB NOW!",
          de: "Magier LB JETZT!",
          fr: "LB MAINTENANT !",
          ja: "\u30AD\u30E3\u30B9LB\uFF01",
          cn: "\u6CD5\u7CFBLB!",
          ko: "\uCE90\uC2A4\uD130 \uB9AC\uBC0B!",
          tc: "\u6CD5\u7CFBLB!"
        }
      }
    },
    {
      id: "UWU Healer LB",
      type: "Ability",

      netRegex: { id: "2B73", source: "Lahabrea", capture: false },
      condition: (pull) => Tool.isHealerJob(pull.job) && pull.beyondLimits.has(pull.me),
      suppressSeconds: 5,
      alertText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Healer LB NOW!",
          de: "Heiler LB JETZT!",
          fr: "Healer LB MAINTENANT !",
          ja: "\u30D2\u30E9LB\uFF01",
          cn: "\u5976\u5988LB!",
          ko: "\uD790\uB7EC \uB9AC\uBC0B!",
          tc: "\u88DC\u5E2BLB!"
        }
      }
    },
    {
      id: "UWU Melee LB",
      type: "StartsUsing",

      netRegex: { id: "2B74", source: "Lahabrea", capture: false },
      condition: (pull) => Tool.isMeleeDpsJob(pull.job) && pull.beyondLimits.has(pull.me),
      alertText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Melee LB NOW!",
          de: "Nahk\xE4mpfer LB JETZT!",
          fr: "LB melee MAINTENANT !",
          ja: "\u8FD1\u63A5LB\uFF01",
          cn: "\u8FD1\u6218LB!",
          ko: "\uADFC\uB51C \uB9AC\uBC0B!",
          tc: "\u8FD1\u6230LB!"
        }
      }
    },
    {
      id: "UWU Ultima",
      type: "StartsUsing",
      netRegex: { id: "2B8B", capture: false },
      condition: (pull) => pull.role === "tank",
      alarmText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Tank LB NOW",
          de: "JETZT Tank LB",
          fr: "LB Tank MAINTENANT !",
          ja: "\u4ECA\u30BF\u30F3\u30AFLB",
          cn: "\u5766\u514BLB",
          ko: "\uD0F1\uB9AC\uBC0B",
          tc: "\u5766\u514BLB"
        }
      }
    },

    {
      id: "UWU Predation",
      comment: {
        en: '"early safe" here means that you can move before the first Ifrit dash.',
        de: '"fr\xFCh sicher" bedeutet hier, dass man such auch schon for dem ersten Ifrit Dash bewegen kann.',
        fr: `"s\xFBr avant" veut dire que vous pouvez bouger avant le dash d'Ifrit.`,
        cn: '\u8FD9\u91CC\u7684 "\u63D0\u524D\u5B89\u5168" \u6307\u4F60\u53EF\u4EE5\u5728\u4F0A\u5F17\u5229\u7279\u7B2C\u4E00\u6B21\u51B2\u950B\u524D\u79FB\u52A8\u3002',
        ko: '\uC5EC\uAE30\uC11C "\uC548\uC804"\uC774\uB780 \uCCAB \uC774\uD504\uB9AC\uD2B8 \uB3CC\uC9C4 \uC804\uC5D0 \uBBF8\uB9AC \uAC00 \uC788\uC5B4\uB3C4 \uB41C\uB2E4\uB294 \uC758\uBBF8\uC785\uB2C8\uB2E4.',
        tc: '\u9019\u88E1\u7684 "\u63D0\u524D\u5B89\u5168" \u6307\u4F60\u53EF\u4EE5\u5728\u4F0A\u5F17\u5229\u7279\u7B2C\u4E00\u6B21\u885D\u92D2\u524D\u79FB\u52D5\u3002'
      },
      type: "StartsUsing",
      netRegex: { id: "2B76", source: "The Ultima Weapon", capture: false },

      delaySeconds: 10,
      durationSeconds: 5,
      promise: (pull) => {
        pull.combatantData = [];
        const hexCodes = Object.values(pull.bossId);
        pull.combatantData = (sayOverlayHandler({
          call: "getCombatants",
          ids: hexCodes.map((x) => parseInt(x, 16))
        })).combatants;
      },
      alertText: (pull, _hit, voice) => {
        const findBig = (tagTwo) => {
          const hexCode = pull.bossId[tagTwo];
          if (hexCode === void 0)
            return void 0;
          const decCode = parseInt(hexCode, 16);
          return pull.combatantData.find((x) => x.ID === decCode);
        };
        const garud = findBig("garuda");
        const ifrits = findBig("ifrit");
        const titans = findBig("titan");
        const ultim = findBig("ultima");
        if (garud === void 0 || ifrits === void 0 || titans === void 0 || ultim === void 0)
          return;
        const garudaFacing = Facings.xyTo8DirNum(garud.PosX, garud.PosY, centerXBit, centerYBit);
        if (garudaFacing % 2 === 0)
          return;
        let clearFacing = [(garudaFacing + 3) % 8, (garudaFacing + 5) % 8];
        const titanFacing = Facings.xyTo8DirNum(titans.PosX, titans.PosY, centerXBit, centerYBit);
        clearFacing = clearFacing.filter((x) => x !== titanFacing);
        const ultimaFacing = Facings.xyTo8DirNum(ultim.PosX, ultim.PosY, centerXBit, centerYBit);
        const notAdjacentToUltim = clearFacing.filter((x) => {
          const isAdjacentToUltim = x === (ultimaFacing + 1) % 8 || ultimaFacing === (x + 1) % 8;
          return !isAdjacentToUltim;
        });
        if (notAdjacentToUltim.length !== 0)
          clearFacing = clearFacing.filter((x) => notAdjacentToUltim.includes(x));
        const ifritFacing = Facings.xyTo8DirNum(ifrits.PosX, ifrits.PosY, centerXBit, centerYBit);
        const facingWordTable = {
          0: voice.dirN(),
          2: voice.dirE(),
          4: voice.dirS(),
          6: voice.dirW()
        };
        for (const facingTwo of clearFacing) {
          for (const go of [-1, 1]) {
            const finals = (facingTwo + go + 8) % 8;
            if (finals === ultimaFacing)
              continue;
            if (finals % 4 === ifritFacing % 4)
              continue;
            const rotation = go === -1 ? voice.counterclockwise() : voice.clockwise();
            return voice.early({ dir: facingWordTable[facingTwo], rotation });
          }
        }
        const garudaOpposit = (garudaFacing + 4) % 8;
        for (const facingTwo of clearFacing) {
          for (const go of [-1, 1]) {
            const finals = (facingTwo + go + 8) % 8;
            if (finals === ultimaFacing)
              continue;
            if (finals !== garudaOpposit)
              continue;
            const rotation = go === -1 ? voice.counterclockwise() : voice.clockwise();
            return voice.normal({ dir: facingWordTable[facingTwo], rotation });
          }
        }
        for (const facingTwo of clearFacing) {
          for (const go of [-1, 1]) {
            const finals = (facingTwo + go + 8) % 8;
            if (finals === ultimaFacing)
              continue;
            const rotation = go === -1 ? voice.counterclockwise() : voice.clockwise();
            return voice.normal({ dir: facingWordTable[facingTwo], rotation });
          }
        }
      },
      outputStrings: {
        early: {
          en: "${dir} => ${rotation} (early safe)",
          de: "${dir} => ${rotation} (fr\xFCh sicher)",
          fr: "${dir} => ${rotation} (s\xFBr avant)",
          ja: "${dir} => ${rotation} (\u5148\u5B89\u5730)",
          cn: "${dir} => ${rotation} (\u63D0\u524D\u5B89\u5168)",
          ko: "${dir} => ${rotation} (\uC548\uC804)",
          tc: "${dir} => ${rotation} (\u63D0\u524D\u5B89\u5168)"
        },
        normal: {
          en: "${dir} => ${rotation}",
          de: "${dir} => ${rotation}",
          fr: "${dir} => ${rotation}",
          ja: "${dir} => ${rotation}",
          cn: "${dir} => ${rotation}",
          ko: "${dir} => ${rotation}",
          tc: "${dir} => ${rotation}"
        },
        clockwise: Voices.clockwise,
        counterclockwise: Voices.counterclockwise,
        dirN: Voices.dirN,
        dirE: Voices.dirE,
        dirS: Voices.dirS,
        dirW: Voices.dirW
      }
    },

    {
      id: "UWU Suppression Gaol",
      type: "Ability",
      netRegex: { id: "2B6B", source: "Titan" },
      condition: (pull, hit) => pull.phase === "suppression" && pull.me === hit.target,
      alarmText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Gaol on YOU",
          de: "Granitgef\xE4ngnis",
          fr: "Ge\xF4le sur VOUS",
          ja: "\u30B8\u30A7\u30A4\u30EB",
          cn: "\u77F3\u7262\u70B9\u540D",
          ko: "\uB3CC\uAC10\uC625 \uB300\uC0C1\uC790",
          tc: "\u77F3\u7262\u9EDE\u540D"
        }
      }
    },
    {
      id: "UWU Aetherochemical Laser Middle",
      type: "StartsUsing",
      netRegex: { source: "The Ultima Weapon", id: "2B84", capture: false },
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Middle Laser",
          de: "Laser (Mitte)",
          fr: "Laser (Milieu)",
          ja: "\u30EC\u30FC\u30B6\u30FC (\u4E2D\u592E)",
          cn: "\u4E2D\u95F4\u6FC0\u5149",
          ko: "\uAC00\uC6B4\uB370 \uB808\uC774\uC800",
          tc: "\u4E2D\u9593\u96F7\u5C04"
        }
      }
    },
    {
      id: "UWU Aetherochemical Laser Right",
      type: "StartsUsing",
      netRegex: { source: "The Ultima Weapon", id: "2B85", capture: false },
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "North Laser",
          de: "Laser (Norden)",
          fr: "Laser (Nord)",
          ja: "\u30EC\u30FC\u30B6\u30FC (\u5317)",
          cn: "\u4E0A\u534A\u573A\u6FC0\u5149",
          ko: "\uBD81\uCABD \uB808\uC774\uC800",
          tc: "\u5317\u534A\u5834\u96F7\u5C04"
        }
      }
    },
    {
      id: "UWU Aetherochemical Laser Left",
      type: "StartsUsing",
      netRegex: { source: "The Ultima Weapon", id: "2B86", capture: false },
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "East Laser",
          de: "Laser (Osten)",
          fr: "Laser (Est)",
          ja: "\u30EC\u30FC\u30B6\u30FC (\u6771)",
          cn: "\u53F3\u534A\u573A\u6FC0\u5149",
          ko: "\uB3D9\uCABD \uB808\uC774\uC800",
          tc: "\u6771\u534A\u5834\u96F7\u5C04"
        }
      }
    },

    {
      id: "UWU Garuda Finale",
      type: "Ability",
      netRegex: { source: "The Ultima Weapon", id: "2CD3", capture: false },
      condition: (pull) => pull.phase === "finale",
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Garuda",
          de: "Garuda",
          fr: "Garuda",
          ja: "\u30AC\u30EB\u30FC\u30C0",
          cn: "\u8FE6\u697C\u7F57",
          ko: "\uAC00\uB8E8\uB2E4",
          tc: "\u8FE6\u6A13\u7F85"
        }
      }
    },
    {
      id: "UWU Ifrit Finale",
      type: "Ability",
      netRegex: { source: "The Ultima Weapon", id: "2CD4", capture: false },
      condition: (pull) => pull.phase === "finale",
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Ifrit",
          de: "Ifrit",
          fr: "Ifrit",
          ja: "\u30A4\u30D5\u30EA\u30FC\u30C8",
          cn: "\u4F0A\u5F17\u5229\u7279",
          ko: "\uC774\uD504\uB9AC\uD2B8",
          tc: "\u4F0A\u5F17\u5229\u7279"
        }
      }
    },
    {
      id: "UWU Titan Finale",
      type: "Ability",
      netRegex: { source: "The Ultima Weapon", id: "2CD5", capture: false },
      condition: (pull) => pull.phase === "finale",
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: {
          en: "Titan",
          de: "Titan",
          fr: "Titan",
          ja: "\u30BF\u30A4\u30BF\u30F3",
          cn: "\u6CF0\u5766",
          ko: "\uD0C0\uC774\uD0C4",
          tc: "\u6CF0\u5766"
        }
      }
    }
  ],
  timelineReplace: [
    {
      "locale": "de",
      "replaceSync": {
        "Bomb Boulder": "Bomber-Brocken",
        "Chirada": "Chirada",
        "Garuda": "Garuda",
        "Ifrit": "Ifrit",
        "Lahabrea": "Lahabrea",
        "Magitek Bit": "Magitek-Drohne",
        "Spiny Plume": "dornig(?:e|er|es|en) Federsturm",
        "Suparna": "Suparna",
        "The Ultima Weapon": "Ultima-Waffe",
        "Titan": "Titan"
      },
      "replaceText": {
        "Aerial Blast": "Windschlag",
        "Aetheric Boom": "\xC4therknall",
        "Aetherochemical Laser": "\xC4therochemischer Laser",
        "(?<! )Aetheroplasm": "\xC4theroplasma",
        "Apply Viscous": "\xC4theroplasma wirkt",
        "Blight": "Pesthauch",
        "Bury": "Begraben",
        "Ceruleum Vent": "Erdseim-Entl\xFCfter",
        "Citadel Siege": "Belagerung der Zitadelle",
        "Crimson Cyclone": "Zinnober-Zyklon",
        "Dark IV": "Neka",
        "Diffractive Laser": "Diffraktiver Laser",
        "Downburst": "Fallb\xF6e",
        "Earthen Fury": "Gaias Zorn",
        "Eruption": "Eruption",
        "Eye Of The Storm": "Auge des Sturms",
        "Feather Rain": "Federregen",
        "Flaming Crush": "Flammensto\xDF",
        "Freefire": "Schwerer Beschuss",
        "Friction": "Windklinge",
        "Geocrush": "Geo-Sto\xDF",
        "Great Whirlwind": "Windhose",
        "Hellfire": "H\xF6llenfeuer",
        "Homing Lasers": "Leitlaser",
        "Incinerate": "Ein\xE4schern",
        "Infernal Fetters": "Infernofesseln",
        "Inferno Howl": "Brennende Wut",
        "Landslide": "Bergsturz",
        "Light Pillar": "Lichts\xE4ule",
        "Mesohigh": "Meso-Hoch",
        "Mistral Shriek": "Mistral-Schrei",
        "Mistral Song": "Mistral-Song",
        "Mountain Buster": "Bergsprenger",
        "Nail Adds": "Fessel Adds",
        "Radiant Plume": "Scheiterhaufen",
        "Rock Buster": "Steinsprenger",
        "Rock Throw": "Granitgef\xE4ngnis",
        "Searing Wind": "Versengen",
        "Self-detonate": "Zerbersten",
        "Slipstream": "Wirbelstr\xF6mung",
        "Summon Random Primal": "Zuf\xE4llige Primaebeschw\xF6rung",
        "Tank Purge": "Tankreinigung",
        "Tumult": "Urersch\xFCtterung",
        "Ultima(?!\\w)": "Ultima",
        "Ultimate Annihilation": "Ultimative Vernichtung",
        "Ultimate Predation": "Ultimative Pr\xE4dation",
        "Ultimate Suppression": "Ultimative Unterdr\xFCckung",
        "Upheaval": "Urtrauma",
        "Viscous Aetheroplasm": "Viskoses \xC4theroplasma",
        "Vulcan Burst": "Feuersto\xDF",
        "Weight Of The Land": "Gaias Gewicht",
        "Wicked Tornado": "Tornado der Bosheit",
        "Wicked Wheel": "Rad der Bosheit"
      }
    },
    {
      "locale": "fr",
      "replaceSync": {
        "Bomb Boulder": "bombo rocher",
        "Chirada": "Chirada",
        "Garuda": "Garuda",
        "Ifrit": "Ifrit",
        "Lahabrea": "Lahabrea",
        "Magitek Bit": "drone magitek",
        "Spiny Plume": "plume perforante",
        "Suparna": "Suparna",
        "The Ultima Weapon": "Ultima Arma",
        "Titan": "Titan"
      },
      "replaceText": {
        "Aerial Blast": "Rafale a\xE9rienne",
        "Aetheric Boom": "Onde d'\xE9ther",
        "Aetherochemical Laser": "Laser magismologique",
        "(?<! )Aetheroplasm": "\xC9th\xE9roplasma",
        "Apply Viscous": "Debuff \xC9th\xE9roplasma",
        "Blight": "Supplice",
        "Bury": "Impact",
        "Ceruleum Vent": "Exutoire \xE0 C\xE9ruleum",
        "Citadel Siege": "Si\xE8ge de citadelle",
        "Crimson Cyclone": "Cyclone \xE9carlate",
        "Dark IV": "Giga T\xE9n\xE8bres",
        "Diffractive Laser": "Laser diffractif",
        "Downburst": "Rafale descendante",
        "Earthen Fury": "Fureur tellurique",
        "Eruption": "\xC9ruption",
        "Eye Of The Storm": "\u0152il du cyclone",
        "Feather Rain": "Pluie de plumes",
        "Flaming Crush": "Fracas de flammes",
        "Freefire": "Tir d'artillerie lourde",
        "Friction": "Lame de vent",
        "Geocrush": "Broie-terre",
        "Great Whirlwind": "Grand tourbillon",
        "Hellfire": "Flammes de l'enfer",
        "Homing Lasers": "Lasers autoguid\xE9s",
        "Incinerate": "Incin\xE9ration",
        "Infernal Fetters": "Cha\xEEnes infernales",
        "Inferno Howl": "Rugissement infernal",
        "Landslide": "Glissement de terrain",
        "Light Pillar": "Colonne lumineuse",
        "Mesohigh": "Anticyclone de m\xE9so-\xE9chelle",
        "Mistral Shriek": "Cri du mistral",
        "Mistral Song": "Chant du mistral",
        "Mountain Buster": "Casse-montagnes",
        "Nail Adds": "Adds Clou",
        "Radiant Plume": "Panache radiant",
        "Rock Buster": "Casse-roc",
        "Rock Throw": "Jet\xE9 de rocs",
        "Searing Wind": "Carbonisation",
        "Self-detonate": "Auto-atomisation",
        "Slipstream": "Sillage",
        "Summon Random Primal": "Invocation de primordial al\xE9atoire",
        "Tank Purge": "Vidange de r\xE9servoir",
        "Tumult": "Tumulte",
        "Ultima(?!\\w)": "Ultima",
        "Ultimate Annihilation": "Fantasmagorie infernale",
        "Ultimate Predation": "Fantasmagorie pr\xE9datrice",
        "Ultimate Suppression": "Fantasmagorie bestiale",
        "Upheaval": "Bouleversement",
        "Viscous Aetheroplasm": "\xC9th\xE9roplasma poisseux",
        "Vulcan Burst": "Explosion volcanique",
        "Weight Of The Land": "Poids de la terre",
        "Wicked Tornado": "Tornade meurtri\xE8re",
        "Wicked Wheel": "Roue mauvaise"
      }
    },
    {
      "locale": "ja",
      "replaceSync": {
        "Bomb Boulder": "\u30DC\u30E0\u30DC\u30EB\u30C0\u30FC",
        "Chirada": "\u30C1\u30E9\u30FC\u30C0",
        "Garuda": "\u30AC\u30EB\u30FC\u30C0",
        "Ifrit": "\u30A4\u30D5\u30EA\u30FC\u30C8",
        "Lahabrea": "\u30A2\u30B7\u30A8\u30F3\u30FB\u30E9\u30CF\u30D6\u30EC\u30A2",
        "Magitek Bit": "\u9B54\u5C0E\u30D3\u30C3\u30C8",
        "Spiny Plume": "\u30B9\u30D1\u30A4\u30CB\u30FC\u30D7\u30EB\u30FC\u30E0",
        "Suparna": "\u30B9\u30D1\u30EB\u30CA",
        "The Ultima Weapon": "\u30A2\u30EB\u30C6\u30DE\u30A6\u30A7\u30DD\u30F3",
        "Titan": "\u30BF\u30A4\u30BF\u30F3"
      },
      "replaceText": {
        "Aerial Blast": "\u30A8\u30EA\u30A2\u30EB\u30D6\u30E9\u30B9\u30C8",
        "Aetheric Boom": "\u30A8\u30FC\u30C6\u30EB\u6CE2\u52D5",
        "Aetherochemical Laser": "\u9B54\u79D1\u5B66\u30EC\u30FC\u30B6\u30FC",
        "(?<! )Aetheroplasm": "\u30A8\u30FC\u30C6\u30EB\u7206\u96F7",
        "Apply Viscous": "\u5438\u7740\u5F0F\u30A8\u30FC\u30C6\u30EB\u7206\u96F7",
        "Blight": "\u30AF\u30E9\u30A6\u30C0",
        "Bury": "\u885D\u6483",
        "Ceruleum Vent": "\u30BB\u30EB\u30EC\u30A2\u30E0\u30D9\u30F3\u30C8",
        "Citadel Siege": "\u30B7\u30BF\u30C7\u30EB\u30B7\u30FC\u30B8",
        "Crimson Cyclone": "\u30AF\u30EA\u30E0\u30BE\u30F3\u30B5\u30A4\u30AF\u30ED\u30F3",
        "Dark IV": "\u30C0\u30FC\u30B8\u30E3",
        "Diffractive Laser": "\u62E1\u6563\u30EC\u30FC\u30B6\u30FC",
        "Downburst": "\u30C0\u30A6\u30F3\u30D0\u30FC\u30B9\u30C8",
        "Earthen Fury": "\u5927\u5730\u306E\u6012\u308A",
        "Eruption": "\u30A8\u30E9\u30D7\u30B7\u30E7\u30F3",
        "Eye Of The Storm": "\u30A2\u30A4\u30FB\u30AA\u30D6\u30FB\u30B9\u30C8\u30FC\u30E0",
        "Feather Rain": "\u30D5\u30A7\u30B6\u30FC\u30EC\u30A4\u30F3",
        "Flaming Crush": "\u30D5\u30EC\u30A4\u30E0\u30AF\u30E9\u30C3\u30B7\u30E5",
        "Freefire": "\u8A98\u7206",
        "Friction": "\u30A6\u30A3\u30F3\u30C9\u30D6\u30EC\u30FC\u30C9",
        "Geocrush": "\u30B8\u30AA\u30AF\u30E9\u30C3\u30B7\u30E5",
        "Great Whirlwind": "\u5927\u65CB\u98A8",
        "Hellfire": "\u5730\u7344\u306E\u706B\u708E",
        "Homing Lasers": "\u8A98\u5C0E\u30EC\u30FC\u30B6\u30FC",
        "Incinerate": "\u30A4\u30F3\u30B7\u30CD\u30EC\u30FC\u30C8",
        "Infernal Fetters": "\u708E\u7344\u306E\u9396",
        "Inferno Howl": "\u707C\u71B1\u306E\u5486\u543C",
        "Landslide": "\u30E9\u30F3\u30C9\u30B9\u30E9\u30A4\u30C9",
        "Light Pillar": "\u30EA\u30D2\u30C8\u30FB\u30BE\u30A4\u30EC",
        "Mesohigh": "\u30E1\u30BD\u30CF\u30A4",
        "Mistral Shriek": "\u30DF\u30B9\u30C8\u30E9\u30EB\u30B7\u30E5\u30EA\u30FC\u30AF",
        "Mistral Song": "\u30DF\u30B9\u30C8\u30E9\u30EB\u30BD\u30F3\u30B0",
        "Mountain Buster": "\u30DE\u30A6\u30F3\u30C6\u30F3\u30D0\u30B9\u30BF\u30FC",
        "Nail Adds": "\u96D1\u9B5A: \u6954",
        "Radiant Plume": "\u5149\u8F1D\u306E\u708E\u67F1",
        "Rock Buster": "\u30ED\u30C3\u30AF\u30D0\u30B9\u30BF\u30FC",
        "Rock Throw": "\u30B0\u30E9\u30CA\u30A4\u30C8\u30FB\u30B8\u30A7\u30A4\u30EB",
        "Searing Wind": "\u71B1\u98A8",
        "Self-detonate": "\u7206\u767A\u9727\u6563",
        "Slipstream": "\u30B9\u30EA\u30C3\u30D7\u30B9\u30C8\u30EA\u30FC\u30E0",
        "Summon Random Primal": "\u30E9\u30F3\u30C0\u30E0\u86EE\u795E\u3092\u53EC\u559A",
        "Tank Purge": "\u9B54\u5C0E\u30D5\u30EC\u30A2",
        "Tumult": "\u6FC0\u9707",
        "Ultima(?!\\w)": "\u30A2\u30EB\u30C6\u30DE",
        "Ultimate Annihilation": "\u7206\u6483\u306E\u7A76\u6975\u5E7B\u60F3",
        "Ultimate Predation": "\u8FFD\u6483\u306E\u7A76\u6975\u5E7B\u60F3",
        "Ultimate Suppression": "\u4E71\u6483\u306E\u7A76\u6975\u5E7B\u60F3",
        "Upheaval": "\u5927\u6FC0\u9707",
        "Viscous Aetheroplasm": "\u5438\u7740\u7206\u96F7\u8D77\u7206",
        "Vulcan Burst": "\u30D0\u30EB\u30AB\u30F3\u30D0\u30FC\u30B9\u30C8",
        "Weight Of The Land": "\u5927\u5730\u306E\u91CD\u307F",
        "Wicked Tornado": "\u30A6\u30A3\u30B1\u30C3\u30C9\u30C8\u30EB\u30CD\u30FC\u30C9",
        "Wicked Wheel": "\u30A6\u30A3\u30B1\u30C3\u30C9\u30DB\u30A4\u30FC\u30EB"
      }
    },
    {
      "locale": "cn",
      "replaceSync": {
        "Bomb Boulder": "\u7206\u7834\u5CA9\u77F3",
        "Chirada": "\u5999\u7FC5",
        "Garuda": "\u8FE6\u697C\u7F57",
        "Ifrit": "\u4F0A\u5F17\u5229\u7279",
        "Lahabrea": "\u62C9\u54C8\u5E03\u96F7\u4E9A",
        "Magitek Bit": "\u6D6E\u6E38\u70AE\u5C04\u51FA",
        "Spiny Plume": "\u523A\u7FBD",
        "Suparna": "\u7F8E\u7FFC",
        "The Ultima Weapon": "\u7A76\u6781\u795E\u5175",
        "Titan": "\u6CF0\u5766"
      },
      "replaceText": {
        "Aerial Blast": "\u5927\u6C14\u7206\u53D1",
        "Aetheric Boom": "\u4EE5\u592A\u6CE2\u52A8",
        "Aetherochemical Laser": "\u9B54\u79D1\u5B66\u6FC0\u5149",
        "(?<! )Aetheroplasm": "\u4EE5\u592A\u7206\u96F7",
        "Apply Viscous": "\u5438\u9644\u5F0F\u70B8\u5F39",
        "Blight": "\u6BD2\u96FE",
        "Bury": "\u584C\u65B9",
        "Ceruleum Vent": "\u9752\u78F7\u653E\u5C04",
        "Citadel Siege": "\u5821\u5792\u56F4\u653B",
        "Crimson Cyclone": "\u6DF1\u7EA2\u65CB\u98CE",
        "Dark IV": "\u51A5\u6697",
        "Diffractive Laser": "\u6269\u6563\u5C04\u7EBF",
        "Downburst": "\u4E0B\u884C\u7A81\u98CE",
        "Earthen Fury": "\u5927\u5730\u4E4B\u6012",
        "Eruption": "\u5730\u706B\u55B7\u53D1",
        "Eye Of The Storm": "\u53F0\u98CE\u773C",
        "Feather Rain": "\u98DE\u7FCE\u96E8",
        "Flaming Crush": "\u70C8\u7130\u788E\u51FB",
        "Freefire": "\u8BF1\u5BFC\u7206\u70B8",
        "Friction": "\u70C8\u98CE\u5203",
        "Geocrush": "\u5927\u5730\u7C89\u788E",
        "Great Whirlwind": "\u5927\u9F99\u5377\u98CE",
        "Hellfire": "\u5730\u72F1\u4E4B\u706B\u708E",
        "Homing Lasers": "\u8BF1\u5BFC\u5C04\u7EBF",
        "Incinerate": "\u70C8\u7130\u711A\u70E7",
        "Infernal Fetters": "\u706B\u72F1\u4E4B\u9501",
        "Inferno Howl": "\u707C\u70ED\u5486\u54EE",
        "Landslide": "\u5730\u88C2",
        "Light Pillar": "\u5149\u67F1",
        "Mesohigh": "\u4E2D\u9AD8\u538B",
        "Mistral Shriek": "\u5BD2\u98CE\u4E4B\u5578",
        "Mistral Song": "\u5BD2\u98CE\u4E4B\u6B4C",
        "Mountain Buster": "\u5C71\u5D29",
        "Nail Adds": "\u706B\u795E\u67F1",
        "Radiant Plume": "\u5149\u8F89\u708E\u67F1",
        "Rock Buster": "\u788E\u5CA9",
        "Rock Throw": "\u82B1\u5C97\u5CA9\u7262\u72F1",
        "Searing Wind": "\u707C\u70ED",
        "Self-detonate": "\u96FE\u6563\u7206\u53D1",
        "Slipstream": "\u87BA\u65CB\u6C14\u6D41",
        "Summon Random Primal": "\u53EC\u5524\u968F\u673A\u86EE\u795E",
        "Tank Purge": "\u9B54\u5BFC\u6838\u7206",
        "Tumult": "\u6012\u9707",
        "Ultima(?!\\w)": "\u7A76\u6781",
        "Ultimate Annihilation": "\u7206\u51FB\u4E4B\u7A76\u6781\u5E7B\u60F3",
        "Ultimate Predation": "\u8FFD\u51FB\u4E4B\u7A76\u6781\u5E7B\u60F3",
        "Ultimate Suppression": "\u4E71\u51FB\u4E4B\u7A76\u6781\u5E7B\u60F3",
        "Upheaval": "\u5927\u6012\u9707",
        "Viscous Aetheroplasm": "\u5F15\u7206\u5438\u9644\u5F0F\u70B8\u5F39",
        "Vulcan Burst": "\u706B\u795E\u7206\u88C2",
        "Weight Of The Land": "\u5927\u5730\u4E4B\u91CD",
        "Wicked Tornado": "\u90AA\u6C14\u9F99\u5377",
        "Wicked Wheel": "\u90AA\u8F6E\u65CB\u98CE"
      }
    },
    {
      "locale": "tc",
      "replaceSync": {
        "Bomb Boulder": "\u7206\u7834\u5CA9\u77F3",
        "Chirada": "\u5999\u7FC5",
        "Garuda": "\u8FE6\u6A13\u7F85",
        "Ifrit": "\u4F0A\u5F17\u5229\u7279",
        "Lahabrea": "\u62C9\u54C8\u5E03\u96F7\u4E9E",
        "Magitek Bit": "\u6D6E\u6E38\u7832\u5C04\u51FA",
        "Spiny Plume": "\u523A\u7FBD",
        "Suparna": "\u7F8E\u7FFC",
        "The Ultima Weapon": "\u7A76\u6975\u6B66\u5668",
        "Titan": "\u6CF0\u5766"
      },
      "replaceText": {
        "Aerial Blast": "\u5927\u6C23\u7206\u767C",
        "Aetheric Boom": "\u4E59\u592A\u6CE2\u52D5",
        "Aetherochemical Laser": "\u9B54\u79D1\u5B78\u96F7\u5C04",
        "(?<! )Aetheroplasm": "\u4E59\u592A\u7206\u96F7",
        "Apply Viscous": "\u5438\u9644\u5F0F\u70B8\u5F48",
        "Blight": "\u6BD2\u9727",
        "Bury": "\u885D\u64CA",
        "Ceruleum Vent": "\u9752\u78F7\u653E\u5C04",
        "Citadel Siege": "\u5821\u58D8\u570D\u653B",
        "Crimson Cyclone": "\u6DF1\u7D05\u65CB\u98A8",
        "Dark IV": "\u51A5\u6697",
        "Diffractive Laser": "\u64F4\u6563\u96F7\u5C04",
        "Downburst": "\u4E0B\u884C\u7A81\u98A8",
        "Earthen Fury": "\u5927\u5730\u4E4B\u6012",
        "Eruption": "\u5674\u767C",
        "Eye Of The Storm": "\u98B1\u98A8\u773C",
        "Feather Rain": "\u98DB\u7FCE\u96E8",
        "Flaming Crush": "\u70C8\u7130\u788E\u64CA",
        "Freefire": "\u8A98\u5C0E\u7206\u70B8",
        "Friction": "\u70C8\u98A8\u5203",
        "Geocrush": "\u5927\u5730\u649E\u64CA",
        "Great Whirlwind": "\u5927\u65CB\u98A8",
        "Hellfire": "\u5730\u7344\u4E4B\u706B\u708E",
        "Homing Lasers": "\u8A98\u5C0E\u5C04\u7DDA",
        "Incinerate": "\u70C8\u7130\u711A\u71D2",
        "Infernal Fetters": "\u706B\u7344\u4E4B\u9396",
        "Inferno Howl": "\u707C\u71B1\u5486\u54EE",
        "Landslide": "\u5730\u88C2",
        "Light Pillar": "\u5149\u67F1",
        "Mesohigh": "\u4E2D\u9AD8\u58D3",
        "Mistral Shriek": "\u5BD2\u98A8\u4E4B\u562F",
        "Mistral Song": "\u5BD2\u98A8\u4E4B\u6B4C",
        "Mountain Buster": "\u5C71\u5D29",
        "Nail Adds": "\u706B\u795E\u67F1",
        "Radiant Plume": "\u5149\u8F1D\u708E\u67F1",
        "Rock Buster": "\u5CA9\u77F3\u7834\u58DE\u8005",
        "Rock Throw": "\u82B1\u5D17\u5CA9\u7262\u7344",
        "Searing Wind": "\u707C\u71B1",
        "Self-detonate": "\u9727\u6563\u7206\u767C",
        "Slipstream": "\u87BA\u65CB\u6C23\u6D41",
        "Summon Random Primal": "\u53EC\u559A\u96A8\u6A5F\u883B\u795E",
        "Tank Purge": "\u9B54\u5C0E\u706B\u5149",
        "Tumult": "\u6FC0\u9707",
        "Ultima(?!\\w)": "\u6700\u7D42\u7A76\u6975",
        "Ultimate Annihilation": "\u7206\u64CA\u4E4B\u7A76\u6975\u5E7B\u60F3",
        "Ultimate Predation": "\u8FFD\u64CA\u4E4B\u7A76\u6975\u5E7B\u60F3",
        "Ultimate Suppression": "\u4E82\u64CA\u4E4B\u7A76\u6975\u5E7B\u60F3",
        "Upheaval": "\u5927\u6FC0\u9707",
        "Viscous Aetheroplasm": "\u5F15\u7206\u5438\u9644\u5F0F\u70B8\u5F48",
        "Vulcan Burst": "\u706B\u795E\u7206\u88C2",
        "Weight Of The Land": "\u5927\u5730\u91CD\u58D3",
        "Wicked Tornado": "\u90AA\u6C23\u9F8D\u6372",
        "Wicked Wheel": "\u90AA\u8F2A\u65CB\u98A8"
      }
    },
    {
      "locale": "ko",
      "replaceSync": {
        "Bomb Boulder": "\uBC14\uC704\uD3ED\uD0C4",
        "Chirada": "\uCE58\uB77C\uB2E4",
        "Garuda": "\uAC00\uB8E8\uB2E4",
        "Ifrit": "\uC774\uD504\uB9AC\uD2B8",
        "Lahabrea": "\uC544\uC528\uC5D4 \uB77C\uD558\uBE0C\uB808\uC544",
        "Magitek Bit": "\uB9C8\uB3C4 \uBE44\uD2B8",
        "Spiny Plume": "\uAC00\uC2DC\uB3CB\uD78C \uAE43\uD138",
        "Suparna": "\uC218\uD30C\uB974\uB098",
        "The Ultima Weapon": "\uC54C\uD14C\uB9C8 \uC6E8\uD3F0",
        "Titan": "\uD0C0\uC774\uD0C4"
      },
      "replaceText": {
        "Aerial Blast": "\uB300\uAE30 \uD3ED\uBC1C",
        "Aetheric Boom": "\uC5D0\uD14C\uB974 \uD30C\uB3D9",
        "Aetherochemical Laser": "\uB9C8\uACFC\uD559 \uB808\uC774\uC800",
        "(?<! )Aetheroplasm": "\uC5D0\uD14C\uB974 \uD3ED\uB8B0",
        "Apply Viscous": "\uD761\uCC29\uC2DD \uC5D0\uD14C\uB974 \uD3ED\uB8B0",
        "Blight": "\uB3C5\uC548\uAC1C",
        "Bury": "\uCDA9\uACA9",
        "Ceruleum Vent": "\uCCAD\uB9B0 \uBC29\uCD9C",
        "Citadel Siege": "\uACF5\uC131",
        "Crimson Cyclone": "\uC9C4\uD64D \uD68C\uC624\uB9AC",
        "Dark IV": "\uB2E4\uC7C8",
        "Diffractive Laser": "\uD655\uC0B0 \uB808\uC774\uC800",
        "Downburst": "\uD558\uAC15 \uAE30\uB958",
        "Earthen Fury": "\uB300\uC9C0\uC758 \uBD84\uB178",
        "Eruption": "\uC6A9\uC554 \uBD84\uCD9C",
        "Eye Of The Storm": "\uD0DC\uD48D\uC758 \uB208",
        "Feather Rain": "\uAE43\uD138\uBE44",
        "Flaming Crush": "\uD654\uC5FC \uC791\uC5F4",
        "Freefire": "\uC720\uD3ED",
        "Friction": "\uBC14\uB78C\uC758 \uCE7C\uB0A0",
        "Geocrush": "\uB300\uC9C0 \uBD95\uAD34",
        "Great Whirlwind": "\uB300\uC120\uD48D",
        "Hellfire": "\uC9C0\uC625\uC758 \uD654\uC5FC",
        "Homing Lasers": "\uC720\uB3C4 \uB808\uC774\uC800",
        "Incinerate": "\uC18C\uAC01",
        "Infernal Fetters": "\uC5FC\uC625\uC758 \uC0AC\uC2AC",
        "Inferno Howl": "\uC791\uC5F4\uC758 \uD3EC\uD6A8",
        "Landslide": "\uC0B0\uC0AC\uD0DC",
        "Light Pillar": "\uBE5B \uAE30\uB465",
        "Mesohigh": "\uB1CC\uC6B0\uACE0\uAE30\uC555",
        "Mistral Shriek": "\uC0AD\uD48D\uC758 \uBE44\uBA85",
        "Mistral Song": "\uC0AD\uD48D\uC758 \uB178\uB798",
        "Mountain Buster": "\uC0B0 \uCABC\uAC1C\uAE30",
        "Nail Adds": "\uC5FC\uC625\uC758 \uB9D0\uB69D",
        "Radiant Plume": "\uAD11\uD718\uC758 \uBD88\uAE30\uB465",
        "Rock Buster": "\uBC14\uC704 \uCABC\uAC1C\uAE30",
        "Rock Throw": "\uD654\uAC15\uC554 \uAC10\uC625",
        "Searing Wind": "\uC5F4\uD48D",
        "Self-detonate": "\uC790\uAC00\uD3ED\uBC1C",
        "Slipstream": "\uBC18\uB3D9 \uAE30\uB958",
        "Summon Random Primal": "\uBB34\uC791\uC704 \uC57C\uB9CC\uC2E0 \uC18C\uD658",
        "Tank Purge": "\uB9C8\uB3C4 \uD50C\uB808\uC5B4",
        "Tumult": "\uACA9\uC9C4",
        "Ultima(?!\\w)": "\uC54C\uD14C\uB9C8",
        "Ultimate Annihilation": "\uAD81\uADF9\uC758 \uD3ED\uACA9 \uD658\uC0C1",
        "Ultimate Predation": "\uAD81\uADF9\uC758 \uCD94\uACA9 \uD658\uC0C1",
        "Ultimate Suppression": "\uAD81\uADF9\uC758 \uB09C\uACA9 \uD658\uC0C1",
        "Upheaval": "\uB300\uACA9\uC9C4",
        "Viscous Aetheroplasm": "\uD761\uCC29 \uD3ED\uB8B0 \uAE30\uD3ED",
        "Vulcan Burst": "\uD3ED\uB82C \uB09C\uC0AC",
        "Weight Of The Land": "\uB300\uC9C0\uC758 \uBB34\uAC8C",
        "Wicked Tornado": "\uB9C8\uB140\uC758 \uD68C\uC624\uB9AC",
        "Wicked Wheel": "\uB9C8\uB140\uC758 \uC218\uB808\uBC14\uD034"
      }
    }
  ]
};

var uwuTimelineCues = [
  { id: "UWU Diffractive Laser", type: "StartsUsing", netRegex: { id: "2B78", source: "The Ultima Weapon" }, suppressSeconds: 3, response: Response.tankCleave() },
  { id: "UWU Eruption", type: "StartsUsing", netRegex: { id: "2B5A", source: "Ifrit", capture: false }, condition: function (pull) { return pull.phase !== "suppression"; }, suppressSeconds: 5, alertText: function (_pull, _hit, voice) { return voice.text(); }, outputStrings: { text: { en: "Eruption Baits" } } },
];

defineDuty({
  id: "TheWeaponsRefrainUltimate",
  name: "UWU - The Weapon's Refrain",
  category: "Ultimate",
  zoneId: 777,
  boss: "",
  center: { x: 100, y: 100 },
  state: function () {
    var d = cuePut.initData();
    d.triggerSetConfig = { gaolOrder1: "", gaolOrder2: "", gaolOrder3: "", gaolOrder4: "", gaolOrder5: "", gaolOrder6: "", gaolOrder7: "", gaolOrder8: "", gaolOrder9: "", gaolOrder10: "", gaolOrder11: "", gaolOrder12: "", gaolOrder13: "", gaolOrder14: "", gaolOrder15: "", gaolOrder16: "", gaolOrder17: "", gaolOrder18: "", gaolOrder19: "", gaolOrder20: "" };
    return d;
  },
  mechanics: uwuTimelineCues.concat(cuePut.triggers).map(function (t) { return raws(t); }),
});
