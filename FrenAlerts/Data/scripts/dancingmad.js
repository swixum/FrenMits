var phaseTwo = {
  'C24C': 'p2',
  'C3F7': 'p3',
  'C2DC': 'p4',
  'BB40': 'p5',
};
var headSignState = Object.assign({
  'fakeFire': '02A1',
  'trueFire': '02A2',
  'fakeIce': '02A3',
  'trueIce': '02A4',
  'fakeThunder': '02A5',
  'trueThunder': '02A6',
  'tankbuster': '00DA',
  'dorito': '007F',
  'stack': '0080',
  'imageTether': '002D',
}, {
  'sharedBuster': '0103',
  'stackPath': '02CB',
  'conePath': '02CD',
  'spreadPath': '02CC',
  'exdeathTether': '0040',
  'blackHoleTether': '0054',
  'stompStack': '00A1',
  '1': '004F',
  '2': '0050',
  '3': '0051',
  '4': '0052',
  '5': '0053',
  '6': '0054',
  '7': '0055',
  '8': '0056',
});
var mysteryMagicVoiceWords = {
  puddle: {
    en: 'Bait Puddle',
    de: 'Fläche ködern',
    fr: 'Déposez',
    ja: 'AOE誘導',
    cn: '诱导AOE',
    ko: '장판 유도',
    tc: '誘導AOE',
  },
  spread: Voices.spread,
  middle: Voices.goIntoMiddle,
  stack: {
    en: 'Stack',
    de: 'Stacken',
    fr: 'Packez-vous',
    ja: 'スタック',
    cn: '集合',
    ko: '집합',
    tc: '集合',
  },
  trueThunder: {
    en: 'Avoid Tell',
  },
  fakeThunder: {
    en: 'In Line',
  },
  trueIce: {
    en: 'Avoid Tell',
  },
  fakeIce: {
    en: 'In Cone',
  },
  trueIcePuddle: {
    en: '${mech1} + ${mech2} => ${mech3}',
  },
  fakeIcePuddle: {
    en: '${mech1} + ${mech2} => ${mech3}',
  },
  stackTrueIce: {
    en: '${mech} + ${ice}',
  },
  stackFakeIce: {
    en: '${mech} + ${ice}',
  },
  spreadTrueIce: {
    en: '${mech} + ${ice}',
  },
  spreadFakeIce: {
    en: '${mech} + ${ice}',
  },
  trueIceTrueThunder: {
    en: 'Avoid Tells',
  },
  fakeIceTrueThunder: {
    en: 'Cone (only)',
  },
  trueIceFakeThunder: {
    en: 'Line (only)',
  },
  fakeIceFakeThunder: {
    en: 'Cone + Line',
  },
  stackTrueThunder: {
    en: '${mech} + ${thunder}',
  },
  stackFakeThunder: {
    en: '${mech} + ${thunder}',
  },
  spreadTrueThunder: {
    en: '${mech} + ${thunder}',
  },
  spreadFakeThunder: {
    en: '${mech} + ${thunder}',
  },
  stackTrueThunderLook: {
    en: '${mech} + ${thunder} + ${look}',
  },
  stackFakeThunderLook: {
    en: '${mech} + ${thunder} + ${look}',
  },
  spreadTrueThunderLook: {
    en: '${mech} + ${thunder} + ${look}',
  },
  spreadFakeThunderLook: {
    en: '${mech} + ${thunder} + ${look}',
  },
  lookAway: {
    en: 'Look Away From Statue',
  },
  lookAt: {
    en: 'Look At Statue',
  },
};
var trapVoiceWords = {
  you: {
    en: 'YOU',
  },
  knockbackFrom1: {
    en: 'Knockback from ${players}',
  },
  knockbackFrom2: {
    en: 'Knockback from ${players}',
  },
  knockbackFrom3: {
    en: 'Knockback from ${players} => Debuffs',
  },
  knockbackFrom3Sleep: {
    en: 'Knockback from ${players} => Sleep',
  },
  knockbackFrom3Confuse: {
    en: 'Knockback from ${players} => Confuse',
  },
  knockbackFromLater: {
    en: 'Knockback from ${players} (later)',
  },
};
var forsakenVoiceWords = {
  tower: Voices.getTowers,
  avoid: {
    en: 'Avoid towers',
    de: 'Türme vermeiden',
    fr: 'Évitez les tours',
    ja: '塔回避',
    cn: '远离塔',
    ko: '기둥 피하기',
    tc: '遠離塔',
  },
  stackOnYou: Voices.stackOnYou,
  num: {
    en: '${num}: ',
    de: '${num}: ',
    fr: '${num}: ',
    ja: '${num}: ',
    cn: '${num}: ',
    ko: '${num}: ',
    tc: '${num}: ',
  },
  you: {
    en: 'YOU',
  },
  swapTowers: {
    en: '${num}Swap Towers',
  },
  leftTower: {
    en: '${num}Left Tower',
  },
  rightTower: {
    en: '${num}Right Tower',
  },
  cone: {
    en: 'Cone on YOU',
  },
  spread: {
    en: 'AOE on YOU',
  },
  stackOnYouTower: {
    en: '${tower} + ${marker}',
  },
  stackOnPlayer: {
    en: 'Stack is on ${player}',
  },
  stacksOnPlayers: {
    en: 'Stacks on ${players}',
  },
  markerOnYouStacksOnPlayers: {
    en: '${num}${marker} + ${stacks}',
  },
  markerOnYouTower: {
    en: '${num}${marker} + ${tower}',
  },
  baitLeftConeOutOdds: {
    en: '${num}Bait Left Cone Out',
  },
  baitLeftConeEvens: {
    en: '${num}Bait Left Cone Left',
  },
  leftStack: {
    en: '${num}Left Stack + ${avoid}',
  },
  rightStack: {
    en: '${num}Right Stack + ${avoid}',
  },
  beNear: {
    en: 'Be Near',
    de: 'Sei Nahe',
    cn: '站近',
    ko: '가까이 있기',
  },
  beFar: {
    en: 'Be Far',
    de: 'Sei Fern',
    cn: '站远',
    ko: '멀리 있기',
  },
  mechs: {
    en: '${num}${mech1} + ${mech2}',
  },
  bait: {
    en: '${num}Bait Cone Right or Clone Far',
  },
  baitCloneFar: {
    en: '${num}Bait Clone Far',
  },
};
var UMAD_MELEE_CRAFTS = {
  DRG: 1, MNK: 1, SAM: 1, RPR: 1, NIN: 1, VPR: 1, RDM: 1, PCT: 1
};
function umadIsMeleeDp(pull) {
  if (pull.role !== "dps") return false;
  var craft = pull.party.member(pull.me);
  return !!UMAD_MELEE_CRAFTS[craft];
}
var UMAD_TRUE_MELEE_CRAFTS = { DRG: 1, MNK: 1, SAM: 1, RPR: 1, NIN: 1, VPR: 1 };
function umadIsMeleeDpsCraft(craft) {
  return !!UMAD_TRUE_MELEE_CRAFTS[craft];
}

function readCWSequenceFromN(n, facingNums) {
  var readCWDistance = function (open, close) {
    var diffs = close - open;
    return diffs < 0 ? diffs + 4 : diffs;
  };
  return facingNums.slice().sort(function (a, b) {
    return readCWDistance(n, a) - readCWDistance(n, b);
  });
}

var UMAD_CELE_PILLAR = { 2015294: 'fire', 2015295: 'ice', 2015296: 'lightning' };
var UMAD_CELE_ELEMS = ['fire', 'ice', 'lightning'];

function umadCeleRestart(pull) {
  delete pull.p5CeleExpiry;
  delete pull.p5CeleCalled;
  delete pull.p5CeleNoDebuff;
  pull.p5CeleTowers = [];
}

function umadCeleActiveDebuff(pull) {
  var exps = pull.p5CeleExpiry || {};
  var nows = Date.now();
  var elTwo = UMAD_CELE_ELEMS.filter(function (elBit) {
    return exps[elBit] !== undefined && exps[elBit] > nows;
  });
  elTwo.sort(function (a, b) { return exps[b] - exps[a]; });
  return elTwo;
}

function umadCeleLivePillars(pull) {
  var byCode = {};
  var bodies = bodiesAll();
  for (var i = 0; i < bodies.length; i++) {
    var elBit = UMAD_CELE_PILLAR[bodies[i].base];
    if (!elBit) continue;
    byCode[bodies[i].e] = { el: elBit, base: bodies[i].base, x: bodies[i].x, y: bodies[i].y };
  }
  var storeds = pull.p5CeleTowers || [];
  for (var s = 0; s < storeds.length; s++) {
    var t = storeds[s];
    if (byCode[t.id]) continue;
    byCode[t.id] = t;
  }
  var awayTwo = [];
  for (var k in byCode) awayTwo.push(byCode[k]);
  return awayTwo;
}

function umadCeleCwFroms(pillars, openX, openY) {
  var cxBit = 100;
  var cyBit = 100;
  var openAng = Math.atan2(openX - cxBit, cyBit - openY);
  return pillars.slice().sort(function (a, b) {
    var aaBit = Math.atan2(a.x - cxBit, cyBit - a.y);
    var baBit = Math.atan2(b.x - cxBit, cyBit - b.y);
    var daBit = (aaBit - openAng + Math.PI * 4) % (Math.PI * 2);
    var dbBit = (baBit - openAng + Math.PI * 4) % (Math.PI * 2);
    if (daBit < 1e-4) daBit = Math.PI * 2;
    if (dbBit < 1e-4) dbBit = Math.PI * 2;
    return daBit - dbBit;
  });
}

function umadCeleSequence(pull) {
  var pillars = umadCeleLivePillars(pull);
  var debuffTwo = umadCeleActiveDebuff(pull);
  if (pull.p5CeleNoDebuff === undefined)
    pull.p5CeleNoDebuff = debuffTwo.length === 0;

  if (pull.p5CeleNoDebuff) {
    var byBas = {};
    for (var i = 0; i < pillars.length; i++) {
      var b = pillars[i].base;
      if (!byBas[b]) byBas[b] = [];
      byBas[b].push(pillars[i]);
    }
    var dups = null;
    var uniqu = null;
    for (var tagTwo in byBas) {
      if (byBas[tagTwo].length > 1) dups = byBas[tagTwo];
      else if (byBas[tagTwo].length === 1) uniqu = byBas[tagTwo][0];
    }
    if (!dups || !uniqu) return null;
    var sortedDups = umadCeleCwFroms(dups, uniqu.x, uniqu.y);
    var afterTwo = sortedDups[1] || sortedDups[0];
    if (!afterTwo) return null;
    return { kind: 'nodebuff', next: afterTwo.el, have: null, order: [afterTwo.el] };
  }

  var hav = {};
  for (var d = 0; d < debuffTwo.length; d++) hav[debuffTwo[d]] = true;
  var pointElBit = debuffTwo[0];
  var pointPillar = null;
  for (var t = 0; t < pillars.length; t++) {
    if (pillars[t].el === pointElBit) {
      pointPillar = pillars[t];
      break;
    }
  }

  if (!pointPillar) return null;

  var sequence = [];
  var known = {};
  var eligibl = pillars.filter(function (twBit) { return !hav[twBit.el]; });
  var cwBit = umadCeleCwFroms(eligibl, pointPillar.x, pointPillar.y);
  for (var c = 0; c < cwBit.length; c++) {
    if (known[cwBit[c].el]) continue;
    known[cwBit[c].el] = true;
    sequence.push(cwBit[c].el);
  }

  var owneds = debuffTwo.slice().reverse();
  for (var o = 0; o < owneds.length; o++) {
    if (known[owneds[o]]) continue;
    known[owneds[o]] = true;
    sequence.push(owneds[o]);
  }

  if (sequence.length === 0) return null;
  return { kind: 'order', have: pointElBit, order: sequence };
}

var umadCeleVoiceWords = {
  fire: { en: '<red>Fire</red>' },
  ice: { en: '<blue>Ice</blue>' },
  lightning: { en: '<yellow>Lightning</yellow>' },
  unknown: Voices.unknown,
  fullOrder: { en: '${have} on YOU: soak ${first} => ${second} => ${third}' },
  twoOrder: { en: '${have} on YOU: soak ${first} => ${second}' },
  oneOrder: { en: '${have} on YOU: soak ${first}' },
  noDebuff: { en: 'No Debuff: soak ${el}' },
};

function umadCeleSpeaks(pull, voice) {
  if (pull.p5CeleCalled) return;
  if (pull.p5CeleNoDebuff === undefined)
    pull.p5CeleNoDebuff = umadCeleActiveDebuff(pull).length === 0;

  var answer = umadCeleSequence(pull);
  if (!answer || !answer.order || answer.order.length === 0) return;
  pull.p5CeleCalled = true;

  if (answer.kind === 'nodebuff')
    return voice.noDebuff({ el: voice[answer.next]() });

  var hav = answer.have ? voice[answer.have]() : voice.unknown();
  if (answer.order.length >= 3) {
    return voice.fullOrder({
      have: hav,
      first: voice[answer.order[0]](),
      second: voice[answer.order[1]](),
      third: voice[answer.order[2]](),
    });
  }
  if (answer.order.length === 2) {
    return voice.twoOrder({
      have: hav,
      first: voice[answer.order[0]](),
      second: voice[answer.order[1]](),
    });
  }
  return voice.oneOrder({
    have: hav,
    first: voice[answer.order[0]](),
  });
}

var UMAD_BUDDY_PILLAR_SETS = { 1: 1, 2: 1, 3: 1, 8: 1 };
function umadBuddyBand(pull) {
  var buddyLabel = pull.party.buddy(pull.me);
  if (!buddyLabel || buddyLabel === pull.me) return 'unknown';
  var minTwo = pull.pathOfLightMarkers[pull.me];
  var their = pull.pathOfLightMarkers[buddyLabel];
  if (!minTwo || !their || minTwo === 'unknown' || their === 'unknown') return 'unknown';
  return minTwo === their ? 'helper' : 'tower';
}
function umadBuddySignLabel(sign) {
  if (sign === 'stack') return 'Stack';
  if (sign === 'cone') return 'Cone';
  if (sign === 'spread') return 'Circle';
  return '';
}
function umadBuddyPut(pull, voice, putDigit) {
  if (putDigit === 1 && (!pull.buddyGroup || pull.buddyGroup === 'unknown')) {
    var g1Bit = umadBuddyBand(pull);
    if (g1Bit !== 'unknown') pull.buddyGroup = g1Bit;
  }
  var band = pull.buddyGroup || 'unknown';
  var sign = pull.pathOfLightMarkers[pull.me] ?? pull.myPathOfLights.at(-1) ?? 'unknown';
  var labels = umadBuddySignLabel(sign);
  var mineThisPut = band === 'tower'
    ? !!UMAD_BUDDY_PILLAR_SETS[putDigit]
    : band === 'helper'
      ? !UMAD_BUDDY_PILLAR_SETS[putDigit]
      : false;
  var tradeAfter = putDigit === 3 || putDigit === 7;
  if (band === 'unknown')
    return voice.buddyMarkerOnly({ num: putDigit, marker: labels || voice.unknown() });
  if (mineThisPut)
    return tradeAfter
      ? voice.buddyTowerSwapNext({ num: putDigit, marker: labels })
      : voice.buddyTower({ num: putDigit, marker: labels });
  return tradeAfter
    ? voice.buddyHelpSwapNext({ num: putDigit })
    : voice.buddyHelp({ num: putDigit });
}
var buddyVoiceWords = {
  unknown: Voices.unknown,
  buddyTower: { en: '${num}: Tower, ${marker}' },
  buddyHelp: { en: '${num}: Help (follow buddy)' },
  buddyTowerSwapNext: { en: '${num}: Tower ${marker}, then SWAP' },
  buddyHelpSwapNext: { en: '${num}: Help, then SWAP' },
  buddyMarkerOnly: { en: '${num}: ${marker}' },
  baitPast: { en: 'Past, bait between towers (max melee)' },
  baitFuture: { en: 'Future, bait away from towers' },
};
Object.assign(forsakenVoiceWords, buddyVoiceWords);
var centerXBit = 100;
var centerYBit = 100;
var blackHoleVoiceWords = Object.assign({}, Facings.outputStringsCardinalDir, {
  num: { en: '${num}: ' },
  nothing: { en: '${num}' },
  getDirTether: { en: '${num}Get ${dir} Tether' },
  getDirTethers: { en: '${num}Get ${dir1}/${dir2} Tethers' },
  getBothTethers: { en: '${num}Get Both Tethers' },
  keepTether: { en: '${num}Keep Tether' },
  passTether: { en: '${num}Pass Tether' },
  clockwiseOne: { en: 'Clockwise 1' },
  clockwiseTwo: { en: 'Clockwise 2' },
  clockwiseThree: { en: 'Clockwise 3' },
  middleThenGetDirTether: { en: '${num}Middle => Get ${dir} Tether' },
  middleThenGetDirTethers: { en: '${num}Middle => Get ${dir1}/${dir2} Tethers' },
  middleThenGetBothTethers: { en: '${num}Middle => Get Both Tethers' },
  oneBlackHole: { en: '${num}${dir}' },
  twoBlackHoles: { en: '${num}${dir1}/${dir2}' },
  threeBlackHoles: { en: '${num}${dir1}/${dir2}/${dir3}' },
});
var umadLcSymSequence = ['1', 'A', '2', 'B', '3', 'C', '4', 'D'];
var umadLcCwBas  = ['B3', '2B', 'A2', '1A', 'D1', '4D', 'C4', '3C'];
var umadLcCcwBas = ['3C', 'C4', '4D', 'D1', '1A', 'A2', '2B', 'B3'];
function umadLcWrap8s(n) { return ((n % 8) + 8) % 8; }
function umadLcFacing(x, z) {
  var a = Math.atan2(x - centerXBit, centerYBit - z);
  return umadLcWrap8s(Math.round(a / (Math.PI / 4)));
}
function umadLcSpot(clockwis, openSym) {
  var k = umadLcSymSequence.indexOf(openSym);
  if (k < 0) return null;
  var srcs = clockwis ? umadLcCwBas : umadLcCcwBas;
  var awayTwo = [];
  for (var i = 0; i < 8; i++) {
    var spot = clockwis ? umadLcWrap8s(i - k) : umadLcWrap8s(i + k);
    awayTwo.push(srcs[spot]);
  }
  return awayTwo;
}
function umadLcOpenSym(x, z) {
  if (typeof __lcWaymarks !== 'function') return null;
  var wmBit = __lcWaymarks();
  if (!wmBit || wmBit.length === 0) return null;
  var bests = -1, bestDists = 1e18;
  for (var i = 0; i < wmBit.length; i++) {
    var dxBit = wmBit[i][1] - x, dzBit = wmBit[i][2] - z;
    var d = dxBit * dxBit + dzBit * dzBit;
    if (d < bestDists) { bestDists = d; bests = wmBit[i][0]; }
  }
  var table = { '0': 'A', '1': 'B', '2': 'C', '3': 'D', '4': '1', '5': '2', '6': '3', '7': '4' };
  var tagTwo = String(Math.round(bests));
  return table[tagTwo] != null ? table[tagTwo] : null;
}
function umadLcFmts(tpls, repls) {
  var s = tpls || '';
  for (var k in repls) s = s.split('{' + k + '}').join(repls[k]);
  return s;
}
var umadCues = [
{
      id: 'DMU Phase Tracker',
      type: 'StartsUsing',
      netRegex: { id: Object.keys(phaseTwo) },
      run: (pull, hit) => pull.phase = phaseTwo[hit.id] ?? 'unknown',
    },
    {
      id: 'DMU ActorSetPos Tracker',
      type: 'ActorSetPos',
      netRegex: { id: '4[0-9A-Fa-f]{7}', capture: true },
      run: (pull, hit) =>
        pull.actorPositions[hit.id] = {
          x: parseFloat(hit.x),
          y: parseFloat(hit.y),
          heading: parseFloat(hit.heading),
        },
    },
    {
      id: 'DMU P1 Revolting Ruin III',
      type: 'HeadMarker',
      netRegex: { id: headSignState['tankbuster'], capture: true },
      alertText: (pull, hit, voice) => {
        const markThree = hit.target;
        if (markThree === pull.me)
          return voice.cleaveOnYou();
        if (pull.role === 'tank')
          return voice.cleaveSwap({
            player: pull.party.member(markThree),
          });
        if (pull.role === 'healer')
          return voice.cleaveOnPlayer({
            player: pull.party.member(markThree),
          });
        return voice.avoidCleaves();
      },
      outputStrings: {
        in: Voices.in,
        out: Voices.out,
        cleaveOnYou: Voices.tankCleaveOnYou,
        avoidCleaves: Voices.avoidTankCleaves,
        cleaveOnPlayer: {
          en: 'Tank Cleave on ${player}',
        },
        cleaveSwap: {
          en: 'Tank Cleave on ${player}',
        },
      },
    },
    {
      id: 'DMU P1 Graven Image Counter',
      type: 'StartsUsing',
      netRegex: { id: 'BCF2', source: 'Kefka', capture: false },
      run: (pull) => pull.gravenImageCount = pull.gravenImageCount + 1,
    },
    {
      id: 'DMU P1 Graven Image Tether Collect',
      type: 'Tether',
      netRegex: { id: headSignState['imageTether'], capture: true },
      condition: Condition.targetIsYou(),
      delaySeconds: 0.1,
      run: (pull, hit) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === undefined) {
          pull.gravenImageTether = 'unknown';
          return;
        }
        const x = actor.x;
        if (x < 101 && x > 99)
          pull.gravenImageTether = 'pulse';
        else if (x < 103 && x > 101)
          pull.gravenImageTether = 'gravitas';
        else if (x > 125)
          pull.gravenImageTether = 'vitrophyre';
        else if (x < 100)
          pull.gravenImageTether = 'indulgent';
        else if (x < 108 && x > 106)
          pull.gravenImageTether = 'idyllic';
        else
          pull.gravenImageTether = 'unknown';
      },
    },
    {
      id: 'DMU P1 Pulse Wave Tethers',
      type: 'Tether',
      netRegex: { id: headSignState['imageTether'], capture: true },
      condition: (pull, hit) => {
        return pull.me === hit.target && pull.gravenImageCount === 1;
      },
      delaySeconds: 0.1,
      durationSeconds: 7,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === undefined)
          return voice.tetherOnYou();
        const x = actor.x;
        if (x < 101 && x > 99)
          return voice.pulse();
        return voice.tetherOnYou();
      },
      outputStrings: {
        tetherOnYou: {
          en: 'Tether on YOU',
          de: 'Verbindung auf DIR',
          fr: 'Lien sur VOUS',
          ja: '線ついた',
          cn: '连线点名',
          ko: '선 대상자 지정됨',
          tc: '連線點名',
        },
        pulse: Voices.knockback,
      },
    },
    {
      id: 'DMU P1 Mystery Magic Collect',
      type: 'HeadMarker',
      netRegex: {
        id: [
          headSignState['trueFire'],
          headSignState['trueIce'],
          headSignState['trueThunder'],
          headSignState['fakeFire'],
          headSignState['fakeIce'],
          headSignState['fakeThunder'],
        ],
        capture: true,
      },
      run: (pull, hit) => {
        switch (hit.id) {
          case headSignState['trueFire']:
            pull.isFireTrue = true;
            return;
          case headSignState['fakeFire']:
            pull.isFireTrue = false;
            return;
          case headSignState['trueIce']:
            pull.isIceTrue = true;
            return;
          case headSignState['fakeIce']:
            pull.isIceTrue = false;
            return;
          case headSignState['trueThunder']:
            pull.isThunderTrue = true;
            return;
          case headSignState['fakeThunder']:
            pull.isThunderTrue = false;
            return;
        }
      },
    },
    {
      id: 'DMU P1 Fire Head Marker Collect',
      type: 'HeadMarker',
      netRegex: { id: [headSignState['dorito'], headSignState['stack']], capture: true },
      suppressSeconds: 2,
      run: (pull, hit) => pull.fireMarker = hit.id,
    },
    {
      id: 'DMU P1 Mystery Magic Ice and Fire',
      type: 'StartsUsing',
      netRegex: { id: 'BA94', source: 'Kefka', capture: false },
      condition: (pull) => {
        return pull.isIceTrue !== undefined && pull.isFireTrue !== undefined
          && pull.phase !== 'p4' && pull.phase !== 'p5';
      },
      infoText: (pull, _hit, voice) => {
        const fireSign = pull.fireMarker;
        if (
          (fireSign === headSignState['dorito'] && pull.isFireTrue) ||
          (fireSign === headSignState['stack'] && !pull.isFireTrue)
        )
          return pull.isIceTrue
            ? voice.spreadTrueIce({ mech: voice.spread(), ice: voice.trueIce() })
            : voice.spreadFakeIce({ mech: voice.spread(), ice: voice.fakeIce() });
        if (
          (fireSign === headSignState['dorito'] && !pull.isFireTrue) ||
          (fireSign === headSignState['stack'] && pull.isFireTrue)
        ) {
          return pull.isIceTrue
            ? voice.stackTrueIce({ mech: voice.stack(), ice: voice.trueIce() })
            : voice.stackFakeIce({ mech: voice.stack(), ice: voice.fakeIce() });
        }
      },
      outputStrings: mysteryMagicVoiceWords,
    },
    {
      id: 'DMU P1 Graven Image Tether Cleanup',
      type: 'Ability',
      netRegex: {
        id: ['BAA9', 'BAAC', 'BAB0', 'BAB5', 'BAB6'],
        source: 'Graven Image',
        capture: true,
      },
      suppressSeconds: 1,
      run: (pull, hit) => {
        const skillTable = {
          'pulse': 'BAAC',
          'gravitas': 'BAA9',
          'vitrophyre': 'BAB0',
          'indulgent': 'BAB5',
          'idyllic': 'BAB6',
          'unknown': 'unknown',
        };
        const leash = pull.gravenImageTether ?? 'unknown';
        const leashSkillCode = skillTable[leash];
        if (leashSkillCode === hit.id || leash === 'unknown')
          delete pull.gravenImageTether;
      },
    },
    {
      id: 'DMU P1 Graven Image Collect',
      type: 'ActorControlExtra',
      netRegex: { category: '019D', param1: '40', param2: '80', capture: true },
      preRun: (pull, hit) => {
        const id = parseInt(hit.id, 16);
        const bluePillars = [id, id - 1];
        const purplePillars = [id - 2, id - 4];
        const yellowPillars = [id - 3, id - 5];
        const eyePillars = [id - 7, id - 9];
        const fakeEyePillars = [id - 6, id - 8];
        const toWordCode = (id) => {
          return id.toString(16).toUpperCase();
        };
        pull.blueTowerIds = bluePillars.map((id) => toWordCode(id));
        pull.purpleTowerIds = purplePillars.map((id) => toWordCode(id));
        pull.yellowTowerIds = yellowPillars.map((id) => toWordCode(id));
        pull.eyeTowerIds = eyePillars.map((id) => toWordCode(id));
        pull.fakeEyeTowerIds = fakeEyePillars.map((id) => toWordCode(id));
      },
      suppressSeconds: 99999,
    },
    {
      id: 'DMU P1 Wave Cannon',
      type: 'ActorControlExtra',
      netRegex: { category: '019D', param1: '40', param2: '80', capture: false },
      suppressSeconds: 99999,
      alertText: (_pull, _hit, voice) => voice.waveCannonLine(),
      outputStrings: {
        waveCannonLine: {
          en: 'E/W Spread',
        },
      },
    },
    {
      id: 'DMU P1 Wave Cannon Collect',
      type: 'Ability',
      netRegex: { id: 'BAA8', source: 'Graven Image', capture: true },
      run: (pull, hit) => pull.waveCannonTargets.push(hit.target),
    },
    {
      id: 'DMU P1 Double-trouble Trap Collect',
      type: 'GainsEffect',
      netRegex: { effectId: '13D6', capture: true },
      run: (pull, hit) => pull.doubleTroubleTrapTargets.push(hit.target),
    },
    {
      id: 'DMU P1 Wave Cannon Explosion Towers',
      type: 'Ability',
      netRegex: { id: 'BAA8', source: 'Graven Image', capture: false },
      delaySeconds: 0.1,
      suppressSeconds: 1,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          getTowers: Voices.getTowers,
          avoid: {
            en: 'Avoid towers',
            de: 'Türme vermeiden',
            fr: 'Évitez les tours',
            ja: '塔回避',
            cn: '远离塔',
            ko: '기둥 피하기',
            tc: '遠離塔',
          },
          extra: {
            en: 'Extra Tower',
          },
        };
        const avoidedCannons = pull.waveCannonTargets.indexOf(pull.me) !== -1;
        if (avoidedCannons && pull.waveCannonTargets.length > 4)
          return { infoText: voice.extra() };
        if (avoidedCannons)
          return { alertText: voice.avoid() };
        return { alertText: voice.getTowers() };
      },
    },
    {
      id: 'DMU P1 Double-trouble Trap Knockback',
      type: 'GainsEffect',
      netRegex: { effectId: '13D6', capture: true },
      delaySeconds: (_pull, hit) => parseFloat(hit.duration) - 3.9,
      durationSeconds: 3.9,
      suppressSeconds: 1,
      response: (pull, hit, voice) => {
        voice.responseOutputStrings = trapVoiceWords;
        if (pull.doubleTroubleTrapTargets.length === 0)
          return;
        const severities = pull.doubleTroubleTrapTargets.includes(pull.me) ? 'alertText' : 'infoText';
        const members = pull.doubleTroubleTrapTargets.map(
          (player) => {
            if (player === pull.me)
              return voice.you();
            return pull.party.member(player);
          },
        );
        const msgs = members?.join(', ');
        const durations = parseFloat(hit.duration);
        if (durations < 6)
          return { [severities]: voice.knockbackFrom1({ players: msgs }) };
        if (durations > 67)
          return { [severities]: voice.knockbackFrom2({ players: msgs }) };
        if (pull.gravenImageTether === 'idyllic')
          return { [severities]: voice.knockbackFrom3Sleep({ players: msgs }) };
        if (pull.gravenImageTether === 'indulgent')
          return { [severities]: voice.knockbackFrom3Confuse({ players: msgs }) };
        return { [severities]: voice.knockbackFrom3({ players: msgs }) };
      },
    },
    {
      id: 'DMU P1 Double-trouble Trap Cleanup',
      type: 'LosesEffect',
      netRegex: { effectId: '13D6', capture: true },
      run: (pull, hit) => {
        pull.doubleTroubleTrapTargets = pull.doubleTroubleTrapTargets.filter(
          (markThree) => markThree !== hit.target,
        );
      },
    },
    {
      id: 'DMU P1 Double-trouble Trap 2 Early',
      type: 'GainsEffect',
      netRegex: { effectId: '13D6', capture: true },
      delaySeconds: 0.3,
      suppressSeconds: 1,
      infoText: (pull, hit, voice) => {
        if (parseFloat(hit.duration) < 67)
          return;
        if (pull.doubleTroubleTrapTargets.length === 0)
          return;
        const members = pull.doubleTroubleTrapTargets.map(
          (player) => {
            if (player === pull.me)
              return voice.you();
            return pull.party.member(player);
          },
        );
        const msgs = members?.join(', ');
        return voice.knockbackFromLater({ players: msgs });
      },
      outputStrings: trapVoiceWords,
    },
    {
      id: 'DMU P1 and P4 Mystery Magic Ice and Thunder',
      type: 'StartsUsing',
      netRegex: { id: 'BA94', source: 'Kefka', capture: false },
      condition: (pull) => {
        return pull.isIceTrue !== undefined && pull.isThunderTrue !== undefined;
      },
      infoText: (pull, _hit, voice) => {
        if (pull.isThunderTrue) {
          return pull.isIceTrue
            ? voice.trueIceTrueThunder()
            : voice.fakeIceTrueThunder();
        }
        return pull.isIceTrue
          ? voice.trueIceFakeThunder()
          : voice.fakeIceFakeThunder();
      },
      outputStrings: mysteryMagicVoiceWords,
    },
    {
      id: 'DMU P1 Light of Judgment',
      type: 'StartsUsing',
      netRegex: { id: 'C622', source: 'Kefka', capture: false },
      response: Response.bigAoe(),
    },
    {
      id: 'DMU P1 Hyperdrive',
      type: 'StartsUsing',
      netRegex: { id: 'C622', source: 'Kefka', capture: true },
      delaySeconds: (_pull, hit) => parseFloat(hit.castTime) - 2,
      response: Response.tankBuster(),
    },
    {
      id: 'DMU P1 Mystery Magic Ice, and Gravitas and Vitrophyre Tethers 1',
      type: 'StartsUsing',
      netRegex: { id: 'BA95', source: 'Kefka', capture: false },
      condition: (pull) => {
        if (
          pull.isIceTrue !== undefined &&
          pull.isThunderTrue === undefined &&
          pull.isFireTrue === undefined
        )
          return true;
        return false;
      },
      infoText: (pull, _hit, voice) => {
        const hasVitrophyr = pull.gravenImageTether === 'vitrophyre';
        return pull.isIceTrue
          ? voice.trueIcePuddle({
            mech1: voice.trueIce(),
            mech2: voice.puddle(),
            mech3: hasVitrophyr ? voice.spread() : voice.middle(),
          })
          : voice.fakeIcePuddle({
            mech1: voice.fakeIce(),
            mech2: voice.puddle(),
            mech3: hasVitrophyr ? voice.spread() : voice.middle(),
          });
      },
      outputStrings: mysteryMagicVoiceWords,
    },
    {
      id: 'DMU P1 Vitrophyre',
      type: 'Ability',
      netRegex: { id: 'BAAC', source: 'Graven Image', capture: false },
      suppressSeconds: 1,
      alertText: (pull, _hit, voice) => {
        if (pull.gravenImageTether === 'vitrophyre')
          return voice.spread();
        return voice.avoidTethers();
      },
      outputStrings: {
        avoidTethers: {
          en: 'Avoid Tethered Players',
        },
        spread: {
          en: 'Spread (avoid puddles)',
        },
      },
    },
    {
      id: 'DMU P1 Double-trouble Trap 3 Early',
      type: 'GainsEffect',
      netRegex: { effectId: '13D6', capture: true },
      delaySeconds: 0.3,
      suppressSeconds: 1,
      infoText: (pull, hit, voice) => {
        const durations = parseFloat(hit.duration);
        if (durations < 48 || durations > 50)
          return;
        if (pull.doubleTroubleTrapTargets.length === 0)
          return;
        const members = pull.doubleTroubleTrapTargets.map(
          (player) => {
            if (player === pull.me)
              return voice.you();
            return pull.party.member(player);
          },
        );
        const msgs = members?.join(', ');
        return voice.knockbackFromLater({ players: msgs });
      },
      outputStrings: trapVoiceWords,
    },
    {
      id: 'DMU P1 Impertinent Will',
      type: 'ActorControlExtra',
      netRegex: { category: '019D', param1: '40', param2: '80', capture: true },
      condition: (pull, hit) => pull.yellowTowerIds.includes(hit.id),
      alertText: (_pull, _hit, voice) => voice.goWest(),
      outputStrings: {
        goWest: Voices.getLeftAndWest,
      },
    },
    {
      id: 'DMU P1 Gravitational Wave',
      type: 'ActorControlExtra',
      netRegex: { category: '019D', param1: '40', param2: '80', capture: true },
      condition: (pull, hit) => pull.purpleTowerIds.includes(hit.id),
      alertText: (_pull, _hit, voice) => voice.goEast(),
      outputStrings: {
        goEast: Voices.getRightAndEast,
      },
    },
    {
      id: 'DMU P1 Gravitas and Vitrophyre Tethers 2',
      type: 'Tether',
      netRegex: { id: headSignState['imageTether'], capture: true },
      condition: (pull, hit) => {
        return pull.me === hit.target &&
          pull.isIceTrue !== undefined &&
          pull.isThunderTrue === undefined &&
          pull.isFireTrue === undefined;
      },
      delaySeconds: 2,
      durationSeconds: 6,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === undefined)
          return voice.tetherOnYou();
        const x = actor.x;
        if (x < 103 && x > 101)
          return voice.gravitas({
            mech1: voice.puddle(),
            mech2: voice.middle(),
          });
        if (x > 125)
          return voice.vitrophyre({
            mech1: voice.puddle(),
            mech2: voice.spread(),
          });
        return voice.tetherOnYou();
      },
      outputStrings: {
        puddle: {
          en: 'Bait Puddle',
          de: 'Fläche ködern',
          fr: 'Déposez',
          ja: 'AOE誘導',
          cn: '诱导AOE',
          ko: '장판 유도',
          tc: '誘導AOE',
        },
        middle: Voices.goIntoMiddle,
        spread: Voices.spread,
        tetherOnYou: {
          en: 'Tether on YOU',
          de: 'Verbindung auf DIR',
          fr: 'Lien sur VOUS',
          ja: '線ついた',
          cn: '连线点名',
          ko: '선 대상자 지정됨',
          tc: '連線點名',
        },
        gravitas: {
          en: '${mech1} => ${mech2}',
        },
        vitrophyre: {
          en: '${mech1} => ${mech2}',
        },
        indulgent: {
          en: 'Confuse Tether on YOU',
        },
        idyllic: {
          en: 'Sleep Tether on YOU',
        },
      },
    },
    {
      id: 'DMU P1 Tele-Portent Collect',
      type: 'GainsEffect',
      netRegex: {
        effectId: [
          '130C',
          '130D',
          '130E',
          '130F',
          '13D7',
          '13D8',
          '13D9',
          '13DA',
        ],
        capture: true,
      },
      condition: Condition.targetIsYou(),
      run: (pull, hit) => {
        const auraTable = {
          '130C': 'up',
          '130D': 'down',
          '130E': 'right',
          '130F': 'left',
          '13D7': 'up',
          '13D8': 'down',
          '13D9': 'right',
          '13DA': 'left',
        };
        const durations = parseFloat(hit.duration);
        if (durations < 8) {
          pull.myTelePortent1 = auraTable[hit.effectId];
          return;
        }
        pull.myTelePortent2 = auraTable[hit.effectId];
      },
    },
    {
      id: 'DMU P1 Tele-Portents',
      type: 'GainsEffect',
      netRegex: {
        effectId: [
          '130C',
          '130D',
          '130E',
          '130F',
          '13D7',
          '13D8',
          '13D9',
          '13DA',
        ],
        capture: true,
      },
      condition: Condition.targetIsYou(),
      durationSeconds: 7,
      infoText: (pull, _hit, voice) => {
        const tp1s = pull.myTelePortent1;
        const tp2s = pull.myTelePortent2;
        if (tp1s === undefined || tp2s === undefined)
          return;
        const portent = tp1s + tp2s;

        if (pull.triggerSetConfig.teleportent === 'clockwise') {
          const dir1Table = {
            'upup': 'west',
            'downdown': 'east',
            'rightright': 'north',
            'leftleft': 'south',
            'downleft': 'dirESE',
            'downright': 'northeast',
            'rightup': 'northwest',
            'rightdown': 'dirNNE',
            'leftup': 'dirSSW',
            'leftdown': 'southeast',
            'upright': 'dirWNW',
            'upleft': 'southwest',
          };
          const dir2Table = {
            'upup': 'south',
            'downdown': 'north',
            'rightright': 'west',
            'leftleft': 'east',
            'downleft': 'south',
            'downright': 'west',
            'rightup': 'south',
            'rightdown': 'east',
            'leftup': 'west',
            'leftdown': 'north',
            'upright': 'north',
            'upleft': 'east',
          };
          const dir1s = dir1Table[portent];
          const dir2s = dir2Table[portent];
          return voice.clockwise({
            dir1: voice[dir1s ?? 'unknown'](),
            dir2: voice[dir2s ?? 'unknown'](),
          });
        }
        if (pull.triggerSetConfig.teleportent === 'filipino') {
          const dir1Table = {
            'upup': 'southeastOut',
            'downdown': 'northwestOut',
            'rightright': 'southwestOut',
            'leftleft': 'northeastOut',
            'downleft': 'dirWSW',
            'downright': 'southeastIn',
            'rightup': 'northeastIn',
            'rightdown': 'dirSSE',
            'leftup': 'dirNNW',
            'leftdown': 'southwestIn',
            'upright': 'dirENE',
            'upleft': 'northwestIn',
          };
          const dir2Table = {
            'upup': 'north',
            'downdown': 'south',
            'rightright': 'east',
            'leftleft': 'west',
            'downleft': 'east',
            'downright': 'south',
            'rightup': 'east',
            'rightdown': 'north',
            'leftup': 'south',
            'leftdown': 'west',
            'upright': 'west',
            'upleft': 'north',
          };
          const dir1s = dir1Table[portent];
          const dir2s = dir2Table[portent];
          return voice.filipino({
            dir1: voice[dir1s ?? 'unknown'](),
            dir2: voice[dir2s ?? 'unknown'](),
          });
        }
        return voice[portent]();
      },
      outputStrings: Object.assign({}, Facings.outputStrings16Dir, {
        north: Voices.north,
        northeast: Voices.northeast,
        east: Voices.east,
        southeast: Voices.southeast,
        south: Voices.south,
        southwest: Voices.southwest,
        west: Voices.west,
        northwest: Voices.northwest,
        unknown: Voices.unknown,
        upup: { en: 'Up Portents' },
        downdown: { en: 'Down Portents' },
        rightright: { en: 'Right Portents' },
        leftleft: { en: 'Left Portents' },
        downleft: { en: 'Down => Left Portent' },
        downright: { en: 'Down => Right Portent' },
        rightup: { en: 'Right => Up Portent' },
        rightdown: { en: 'Right => Down Portent' },
        leftup: { en: 'Left => Up Portent' },
        leftdown: { en: 'Left => Down Portent' },
        upright: { en: 'Up => Right Portent' },
        upleft: { en: 'Up => Left Portent' },
        clockwise: { en: '${dir1} => ${dir2}' },
        filipino: { en: '${dir1} => ${dir2}' },
        southeastOut: { en: 'Southeast Out' },
        northwestOut: { en: 'Northwest Out' },
        southwestOut: { en: 'Southwest Out' },
        northeastOut: { en: 'Northeast Out' },
        southeastIn: { en: 'Southeast In' },
        northeastIn: { en: 'Northeast In' },
        southwestIn: { en: 'Southwest In' },
        northwestIn: { en: 'Northwest In' },
      }),
    },
    {
      id: 'DMU P1 Tele-Portent 2',
      type: 'LosesEffect',
      netRegex: {
        effectId: [
          '130C',
          '130D',
          '130E',
          '130F',
          '13D7',
          '13D8',
          '13D9',
          '13DA',
        ],
        capture: true,
      },
      condition: (pull, hit) => {
        if (pull.me === hit.target)
          if (pull.myTelePortent1 !== undefined)
            return true;
        return false;
      },
      durationSeconds: 3,
      response: Response.moveAway('alert'),
    },
    {
      id: 'DMU P1 Tele-Portent Cleanup',
      type: 'LosesEffect',
      netRegex: {
        effectId: [
          '130C',
          '130D',
          '130E',
          '130F',
          '13D7',
          '13D8',
          '13D9',
          '13DA',
        ],
        capture: true,
      },
      condition: Condition.targetIsYou(),
      suppressSeconds: 1,
      run: (pull) => {
        delete pull.myTelePortent1;
        delete pull.myTelePortent2;
      },
    },
    {
      id: 'DMU P1 Indulgent Will and Idyllic Will Tethers (Early)',
      type: 'Tether',
      netRegex: { id: headSignState['imageTether'], capture: true },
      condition: (pull, hit) => {
        return pull.me === hit.target && pull.gravenImageCount === 3;
      },
      delaySeconds: 0.1,
      durationSeconds: 5.5,
      infoText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === undefined)
          return voice.tetherOnYou();
        const x = actor.x;
        if (x < 100)
          return voice.indulgent();
        if (x < 108 && x > 106)
          return voice.idyllic();
        return voice.tetherOnYou();
      },
      outputStrings: {
        tetherOnYou: {
          en: 'Tether on YOU',
          de: 'Verbindung auf DIR',
          fr: 'Lien sur VOUS',
          ja: '線ついた',
          cn: '连线点名',
          ko: '선 대상자 지정됨',
          tc: '連線點名',
        },
        indulgent: {
          en: 'Confuse Tether on YOU',
        },
        idyllic: {
          en: 'Sleep Tether on YOU',
        },
      },
    },
    {
      id: 'DMU P1 Indulgent Will and Idyllic Will Tethers Reminder',
      type: 'Tether',
      netRegex: { id: headSignState['imageTether'], capture: true },
      condition: (pull, hit) => {
        return pull.me === hit.target && pull.gravenImageCount === 3;
      },
      delaySeconds: 5.6,
      durationSeconds: 4,
      alertText: (pull, hit, voice) => {
        const actor = pull.actorPositions[hit.sourceId];
        if (actor === undefined)
          return voice.tetherOnYou();
        const x = actor.x;
        if (x < 100)
          return voice.indulgent();
        if (x < 108 && x > 106)
          return voice.idyllic();
        return voice.tetherOnYou();
      },
      outputStrings: {
        tetherOnYou: {
          en: 'Tether on YOU',
          de: 'Verbindung auf DIR',
          fr: 'Lien sur VOUS',
          ja: '線ついた',
          cn: '连线点名',
          ko: '선 대상자 지정됨',
          tc: '連線點名',
        },
        indulgent: {
          en: 'Confuse Tether on YOU',
        },
        idyllic: {
          en: 'Sleep Tether on YOU',
        },
      },
    },
    {
      id: 'DMU P1 Ave Maria / Indolent Will Collect',
      type: 'ActorControlExtra',
      netRegex: { category: '019D', param1: '40', param2: '80', capture: true },
      run: (pull, hit) => {
        const id = hit.id;
        if (pull.eyeTowerIds.includes(id) || pull.fakeEyeTowerIds.includes(hit.id))
          pull.isTowerLookAway = pull.eyeTowerIds.includes(id);
      },
    },
    {
      id: 'DMU P1 Ave Maria (Early)',
      type: 'ActorControlExtra',
      netRegex: { category: '019D', param1: '40', param2: '80', capture: true },
      condition: (pull, hit) => pull.fakeEyeTowerIds.includes(hit.id),
      durationSeconds: 4.7,
      infoText: (_pull, _hit, voice) => voice.lookAtLater(),
      outputStrings: {
        lookAtLater: {
          en: 'Look At Statue (later)',
          ko: '시선 바라보기 (나중에)',
        },
      },
    },
    {
      id: 'DMU P1 Indolent Will (Early)',
      type: 'ActorControlExtra',
      netRegex: { category: '019D', param1: '40', param2: '80', capture: true },
      condition: (pull, hit) => pull.eyeTowerIds.includes(hit.id),
      durationSeconds: 4.7,
      infoText: (_pull, _hit, voice) => voice.lookAwayLater(),
      outputStrings: {
        lookAwayLater: {
          en: 'Look Away From Statue (later)',
          ko: '시선 피하기 (나중에)',
        },
      },
    },
    {
      id: 'DMU P1 Mystery Magic Fire and Thunder',
      type: 'StartsUsing',
      netRegex: { id: 'BA94', source: 'Kefka', capture: false },
      condition: (pull) => {
        return pull.isFireTrue !== undefined && pull.isThunderTrue !== undefined
          && pull.phase !== 'p4' && pull.phase !== 'p5';
      },
      infoText: (pull, _hit, voice) => {
        const fireSign = pull.fireMarker;
        const glance = pull.isTowerLookAway ? voice.lookAway() : voice.lookAt();
        if (
          (fireSign === headSignState['dorito'] && pull.isFireTrue) ||
          (fireSign === headSignState['stack'] && !pull.isFireTrue)
        )
          return pull.isThunderTrue
            ? voice.spreadTrueThunderLook({
              mech: voice.spread(),
              thunder: voice.trueThunder(),
              look: glance,
            })
            : voice.spreadFakeThunderLook({
              mech: voice.spread(),
              thunder: voice.fakeThunder(),
              look: glance,
            });
        if (
          (fireSign === headSignState['dorito'] && !pull.isFireTrue) ||
          (fireSign === headSignState['stack'] && pull.isFireTrue)
        ) {
          return pull.isThunderTrue
            ? voice.stackTrueThunderLook({
              mech: voice.stack(),
              thunder: voice.trueThunder(),
              look: glance,
            })
            : voice.stackFakeThunderLook({
              mech: voice.stack(),
              thunder: voice.fakeThunder(),
              look: glance,
            });
        }
      },
      outputStrings: mysteryMagicVoiceWords,
    },
    {
      id: 'DMU P4 Shotcall Mystery Magic',
      type: 'StartsUsing',
      netRegex: { id: 'BA94', source: 'Kefka', capture: false },
      condition: (pull) => {
        if (pull.phase !== 'p4')
          return false;
        const put = (pull.isIceTrue !== undefined ? 1 : 0) + (pull.isFireTrue !== undefined ? 1 : 0) +
          (pull.isThunderTrue !== undefined ? 1 : 0);
        return put >= 2;
      },
      macroText: (pull, _hit, voice) => {
        if (pull.isIceTrue !== undefined && pull.isThunderTrue !== undefined) {
          if (pull.isThunderTrue)
            return pull.isIceTrue ? voice.trueIceTrueThunder() : voice.fakeIceTrueThunder();
          return pull.isIceTrue ? voice.trueIceFakeThunder() : voice.fakeIceFakeThunder();
        }
        const isScatter = (pull.fireMarker === headSignState['dorito'] && pull.isFireTrue) ||
          (pull.fireMarker === headSignState['stack'] && !pull.isFireTrue);
        const bit = isScatter ? voice.spread() : voice.stack();
        if (pull.isIceTrue !== undefined)
          return voice.fireIce({ mech: bit, cone: pull.isIceTrue ? voice.avoidCone() : voice.inCone() });
        return pull.isThunderTrue
          ? voice.fireTrueThunder({ mech: bit })
          : voice.fireFakeThunder({ mech: bit });
      },
      outputStrings: {
        trueIceTrueThunder: { en: 'TRUE ice (Cones) / TRUE lightning (Lines)' },
        fakeIceTrueThunder: { en: 'FAKE ice (Cones) / TRUE lightning (Lines)' },
        trueIceFakeThunder: { en: 'TRUE ice (Cones) / FAKE lightning (Lines)' },
        fakeIceFakeThunder: { en: 'FAKE ice (Cones) / FAKE lightning (Lines)' },
        fireTrueThunder: { en: '${mech} / TRUE lightning (Lines)' },
        fireFakeThunder: { en: '${mech} / FAKE lightning (Lines)' },
        fireIce: { en: '${mech} / ${cone}' },
        avoidCone: { en: 'avoid Cone' },
        inCone: { en: 'in Cone' },
        spread: { en: 'Spread' },
        stack: { en: 'Stack' },
      },
    },
    {
      id: 'DMU P1 and P4 Mystery Magic Cleanup',
      type: 'StartsUsing',
      netRegex: { id: ['BA94', 'C622', 'BB14'], source: ['Kefka', 'Neo Exdeath'], capture: false },
      delaySeconds: 0.2,
      run: (pull) => {
        delete pull.isFireTrue;
        delete pull.isIceTrue;
        delete pull.isThunderTrue;
        delete pull.fireMarker;
      },
    },
{
      id: 'DMU P2 Ultimate Embrace',
      type: 'StartsUsing',
      netRegex: { id: 'C24C', source: 'Kefka', capture: true },
      response: Response.sharedTankBuster(),
    },
    {
      id: 'DMU P2 Forsaken',
      type: 'StartsUsing',
      netRegex: { id: 'BABC', source: 'Kefka', capture: false },
      durationSeconds: 6.7,
      response: Response.bigAoe('alert'),
    },
    {
      id: 'DMU P2 Path of Light Headmarker Tracker',
      type: 'HeadMarker',
      netRegex: {
        id: [
          headSignState['stackPath'],
          headSignState['conePath'],
          headSignState['spreadPath'],
        ],
        capture: true,
      },
      run: (pull, hit) => {
        const id = hit.id;
        const markThree = hit.target;
        const signs = {
          '02CB': 'stack',
          '02CD': 'cone',
          '02CC': 'spread',
        };
        if (pull.me === markThree)
          pull.myPathOfLights.push(signs[id] ?? 'unknown');
        pull.pathOfLightMarkers[markThree] = signs[id] ?? 'unknown';
        pull.pathOfLightStackPlayers = pull.pathOfLightStackPlayers.filter((t) => t !== markThree);
        pull.pathOfLightConePlayers = pull.pathOfLightConePlayers.filter((t) => t !== markThree);
        pull.pathOfLightSpreadPlayers = pull.pathOfLightSpreadPlayers.filter((t) => t !== markThree);
        if (pull.pathOfLightCounter === 2 && pull.me === hit.target)
          pull.isForsakenGroupA = true;
        if (pull.pathOfLightCounter === 1 && (!pull.buddyGroup || pull.buddyGroup === 'unknown')) {
          const g = umadBuddyBand(pull);
          if (g !== 'unknown') pull.buddyGroup = g;
        }
        if (id === headSignState['stackPath'])
          pull.pathOfLightStackPlayers.push(markThree);
        else if (id === headSignState['conePath'])
          pull.pathOfLightConePlayers.push(markThree);
        else
          pull.pathOfLightSpreadPlayers.push(markThree);
      },
    },
    {
      id: 'DMU P2 Path of Light Towers 1',
      type: 'HeadMarker',
      netRegex: {
        id: [
          headSignState['stackPath'],
          headSignState['conePath'],
          headSignState['spreadPath'],
        ],
        capture: true,
      },
      condition: (pull, hit) => {
        return pull.me === hit.target && pull.pathOfLightCounter === 1;
      },
      delaySeconds: 0.1,
      durationSeconds: 9,
      infoText: (pull, hit, voice) => {
        const id = hit.id;
        const signs = {
          '02CB': 'stack',
          '02CD': 'cone',
          '02CC': 'spread',
        };
        const sign = signs[id];
        if (sign === undefined)
          return;
        const num = pull.pathOfLightCounter;
        if (pull.triggerSetConfig.forsaken === 'buddy')
          return umadBuddyPut(pull, voice, num);
        if (sign === 'stack') {
          if (pull.triggerSetConfig.forsaken === 'kroxy-rinon') {
            if (pull.role === 'healer' || pull.role === 'tank')
              return voice.stackOnYouTower({
                num: voice.num({ num: num }),
                tower: voice.leftTower(),
                marker: voice.stackOnYou(),
              });
            return voice.stackOnYouTower({
              num: voice.num({ num: num }),
              tower: voice.rightTower(),
              marker: voice.stackOnYou(),
            });
          }
          return voice.stackOnYouTower({
            num: voice.num({ num: num }),
            tower: voice.tower(),
            marker: voice.stackOnYou(),
          });
        }
        const stack1s = pull.pathOfLightStackPlayers[0] ?? 'unknown';
        const stack2s = pull.pathOfLightStackPlayers[1] ?? 'unknown';
        const stack1IsDPSBit = pull.party.isDPS(stack1s);
        const stack2IsDPSBit = pull.party.isDPS(stack2s);
        const myDutyIsDPS = pull.party.isDPS(pull.me);
        if (myDutyIsDPS === stack1IsDPSBit && myDutyIsDPS === stack2IsDPSBit) {
          const members = pull.pathOfLightStackPlayers.map(
            (player) => {
              return pull.party.member(player);
            },
          );
          const msgs = members?.join(', ');
          return voice.markerOnYouStacksOnPlayers({
            num: voice.num({ num: num }),
            marker: voice[sign](),
            stacks: voice.stacksOnPlayers({ players: msgs }),
          });
        }
        const possiblePartners = pull.party.member(myDutyIsDPS === stack1IsDPSBit ? stack1s : stack2s);
        return voice.markerOnYouStacksOnPlayers({
          num: voice.num({ num: num }),
          marker: voice[sign](),
          stacks: voice.stackOnPlayer({ player: possiblePartners }),
        });
      },
      outputStrings: forsakenVoiceWords,
    },
    {
      id: 'DMU P2 Path of Light Counter',
      type: 'Ability',
      netRegex: { id: 'BABE', source: 'Kefka', capture: false },
      suppressSeconds: 1,
      run: (pull) => pull.pathOfLightCounter = pull.pathOfLightCounter + 1,
    },
    {
      id: 'DMU P2 Path of Light Towers 2',
      type: 'HeadMarker',
      netRegex: {
        id: [
          headSignState['stackPath'],
          headSignState['conePath'],
          headSignState['spreadPath'],
        ],
        capture: false,
      },
      condition: (pull) => pull.pathOfLightCounter === 2,
      delaySeconds: 0.1,
      durationSeconds: 9,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const num = pull.pathOfLightCounter;
        if (pull.triggerSetConfig.forsaken === 'buddy')
          return umadBuddyPut(pull, voice, num);
        const sign = pull.myPathOfLights.at(-1) ?? 'unknown';
        if (pull.isForsakenGroupA) {
          const nearFars = sign === 'spread'
            ? voice.beFar()
            : voice.beNear();
          if (pull.triggerSetConfig.forsaken === 'kroxy-rinon') {
            if (pull.role === 'healer' || pull.role === 'tank') {
              if (pull.myPathOfLights[0] === 'cone')
                return voice.mechs({
                  num: voice.num({ num: num }),
                  mech1: voice.tower(),
                  mech2: nearFars,
                });
              if (pull.myPathOfLights[0] === 'spread')
                return voice.mechs({
                  num: voice.num({ num: num }),
                  mech1: voice.swapTowers(),
                  mech2: nearFars,
                });
            }
            if (pull.myPathOfLights[0] === 'cone')
              return voice.mechs({
                num: voice.num({ num: num }),
                mech1: voice.swapTowers(),
                mech2: nearFars,
              });
            if (pull.myPathOfLights[0] === 'spread')
              return voice.mechs({
                num: voice.num({ num: num }),
                mech1: voice.tower(),
                mech2: nearFars,
              });
          }
          return voice.mechs({
            num: voice.num({ num: num }),
            mech1: voice.tower(),
            mech2: nearFars,
          });
        }
        if (pull.role === 'healer')
          return voice.baitLeftConeEvens({
            num: voice.num({ num: num }),
          });
        if (pull.role === 'tank')
          return voice.baitCloneFar({
            num: voice.num({ num: num }),
          });
        return voice.bait({
          num: voice.num({ num: num }),
        });
      },
      outputStrings: forsakenVoiceWords,
    },
    {
      id: 'DMU P2 Future\'s End/Past\'s End (Early)',
      type: 'StartsUsing',
      netRegex: { id: ['BAD2', 'BAD3'], source: 'Kefka', capture: true },
      infoText: (pull, hit, voice) => {
        if (pull.triggerSetConfig.forsaken === 'buddy')
          return hit.id === 'BAD2' ? voice.baitFuture() : voice.baitPast();
        return hit.id === 'BAD2' ? voice.future() : voice.past();
      },
      outputStrings: {
        future: { en: 'Bait Ending opposite Towers' },
        past: { en: 'Bait Ending between Towers' },
        baitFuture: { en: 'Future, bait away from towers' },
        baitPast: { en: 'Past, bait between towers (max melee)' },
      },
    },
    {
      id: 'DMU P2 All Things Ending Baits',
      type: 'Ability',
      netRegex: { id: ['BAD2', 'BAD3'], source: 'Kefka', capture: true },
      delaySeconds: 1.2,
      alertText: (pull, hit, voice) => {
        const isFutur = hit.id === 'BAD2';
        if (pull.triggerSetConfig.forsaken === 'buddy' && pull.pathOfLightCounter !== 9)
          return isFutur ? voice.baitFuture() : voice.baitPast();
        if (pull.pathOfLightCounter !== 9)
          return isFutur ? voice.future() : voice.past();
        return isFutur
          ? voice.lastFuture({ action: voice.behind() })
          : voice.lastPast({ action: voice.stay() });
      },
      outputStrings: {
        behind: Voices.getBehind,
        baitFuture: { en: 'Future, bait away from towers' },
        baitPast: { en: 'Past, bait between towers (max melee)' },
        stay: {
          en: 'Stay',
          de: 'Bleib stehen',
          fr: 'Restez',
          cn: '停',
          ko: '대기',
          tc: '停',
        },
        future: {
          en: 'Bait Ending opposite Towers',
        },
        past: {
          en: 'Bait Ending between Towers',
        },
        lastFuture: {
          en: 'Bait Ending => ${action}',
        },
        lastPast: {
          en: 'Bait Ending => ${action}',
        },
      },
    },
    {
      id: 'DMU P2 Path of Light Towers 3',
      type: 'StartsUsing',
      netRegex: { id: ['BADC', 'BADD'], source: 'Kefka', capture: false },
      condition: (pull) => pull.pathOfLightCounter === 3,
      suppressSeconds: 1,
      alertText: (pull, _hit, voice) => {
        const num = pull.pathOfLightCounter;
        if (pull.triggerSetConfig.forsaken === 'buddy')
          return umadBuddyPut(pull, voice, num);
        const sign = pull.myPathOfLights.at(-1) ?? 'unknown';
        if (pull.isForsakenGroupA) {
          if (sign === 'stack') {
            const members = pull.pathOfLightStackPlayers.map(
              (player) => {
                if (player === pull.me)
                  return voice.you();
                return pull.party.member(player);
              },
            );
            const msgs = members?.join(', ');
            return voice.markerOnYouTower({
              num: voice.num({ num: num }),
              marker: voice.stacksOnPlayers({ players: msgs }),
              tower: voice.tower(),
            });
          }
          if (pull.triggerSetConfig.forsaken === 'kroxy-rinon') {
            return voice.markerOnYouTower({
              num: voice.num({ num: num }),
              marker: voice[sign](),
              tower: sign === 'cone'
                ? voice.leftTower()
                : voice.rightTower(),
            });
          }
          return voice.markerOnYouTower({
            num: voice.num({ num: num }),
            marker: voice[sign](),
            tower: voice.tower(),
          });
        }
        if (pull.role === 'tank')
          return voice.leftStack({
            num: voice.num({ num: num }),
            avoid: voice.avoid(),
          });
        if (pull.role === 'healer')
          return voice.baitLeftConeOutOdds({
            num: voice.num({ num: num }),
          });
        return voice.rightStack({
          num: voice.num({ num: num }),
          avoid: voice.avoid(),
        });
      },
      outputStrings: forsakenVoiceWords,
    },
    {
      id: 'DMU P2 Path of Light Towers 4',
      type: 'HeadMarker',
      netRegex: {
        id: [
          headSignState['stackPath'],
          headSignState['conePath'],
          headSignState['spreadPath'],
        ],
        capture: false,
      },
      condition: (pull) => pull.pathOfLightCounter === 4,
      delaySeconds: 0.1,
      durationSeconds: 9,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const num = pull.pathOfLightCounter;
        if (pull.triggerSetConfig.forsaken === 'buddy')
          return umadBuddyPut(pull, voice, num);
        const sign = pull.myPathOfLights.at(-1) ?? 'unknown';
        if (pull.isForsakenGroupA) {
          if (pull.role === 'healer')
            return voice.baitLeftConeEvens({
              num: voice.num({ num: num }),
            });
          if (pull.role === 'tank')
            return voice.baitCloneFar({
              num: voice.num({ num: num }),
            });
          return voice.bait({
            num: voice.num({ num: num }),
          });
        }
        if (sign === 'stack' || sign === 'unknown')
          return;
        const nearFars = sign === 'spread'
          ? voice.beFar()
          : voice.beNear();
        if (pull.triggerSetConfig.forsaken === 'kroxy-rinon') {
          return voice.mechs({
            mech1: pull.role === 'tank' || umadIsMeleeDp(pull)
              ? voice.rightTower()
              : voice.leftTower(),
            mech2: nearFars,
          });
        }
        return voice.mechs({
          num: voice.num({ num: num }),
          mech1: voice.tower(),
          mech2: nearFars,
        });
      },
      outputStrings: forsakenVoiceWords,
    },
    {
      id: 'DMU P2 Path of Light Towers 5',
      type: 'StartsUsing',
      netRegex: { id: ['BADC', 'BADD'], source: 'Kefka', capture: false },
      condition: (pull) => pull.pathOfLightCounter === 5,
      suppressSeconds: 1,
      alertText: (pull, _hit, voice) => {
        const num = pull.pathOfLightCounter;
        if (pull.triggerSetConfig.forsaken === 'buddy')
          return umadBuddyPut(pull, voice, num);
        const sign = pull.myPathOfLights.at(-1) ?? 'unknown';
        if (pull.isForsakenGroupA) {
          if (pull.role === 'tank')
            return voice.leftStack({
              num: voice.num({ num: num }),
              avoid: voice.avoid(),
            });
          if (pull.role === 'healer')
            return voice.baitLeftConeOutOdds({
              num: voice.num({ num: num }),
            });
          return voice.rightStack({
            num: voice.num({ num: num }),
            avoid: voice.avoid(),
          });
        }
        if (sign === 'stack') {
          const members = pull.pathOfLightStackPlayers.map(
            (player) => {
              if (player === pull.me)
                return voice.you();
              return pull.party.member(player);
            },
          );
          const msgs = members?.join(', ');
          return voice.markerOnYouTower({
            num: voice.num({ num: num }),
            marker: voice.stacksOnPlayers({ players: msgs }),
            tower: voice.tower(),
          });
        }
        if (pull.triggerSetConfig.forsaken === 'kroxy-rinon') {
          return voice.markerOnYouTower({
            num: voice.num({ num: num }),
            marker: voice[sign](),
            tower: sign === 'cone'
              ? voice.leftTower()
              : voice.rightTower(),
          });
        }
        return voice.markerOnYouTower({
          num: voice.num({ num: num }),
          marker: voice[sign](),
          tower: voice.tower(),
        });
      },
      outputStrings: forsakenVoiceWords,
    },
    {
      id: 'DMU P2 Path of Light Towers 6',
      type: 'HeadMarker',
      netRegex: {
        id: [
          headSignState['stackPath'],
          headSignState['conePath'],
          headSignState['spreadPath'],
        ],
        capture: false,
      },
      condition: (pull) => pull.pathOfLightCounter === 6,
      delaySeconds: 0.1,
      durationSeconds: 9,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const num = pull.pathOfLightCounter;
        if (pull.triggerSetConfig.forsaken === 'buddy')
          return umadBuddyPut(pull, voice, num);
        const sign = pull.myPathOfLights.at(-1) ?? 'unknown';
        if (pull.isForsakenGroupA) {
          if (pull.role === 'healer')
            return voice.baitLeftConeEvens({
              num: voice.num({ num: num }),
            });
          if (pull.role === 'tank')
            return voice.baitCloneFar({
              num: voice.num({ num: num }),
            });
          return voice.bait({
            num: voice.num({ num: num }),
          });
        }
        const nearFars = sign === 'spread'
          ? voice.beFar()
          : voice.beNear();
        if (pull.triggerSetConfig.forsaken === 'kroxy-rinon') {
          return voice.mechs({
            num: voice.num({ num: num }),
            mech1: pull.role === 'tank' || umadIsMeleeDp(pull)
              ? voice.rightTower()
              : voice.leftTower(),
            mech2: nearFars,
          });
        }
        return voice.mechs({
          num: voice.num({ num: num }),
          mech1: voice.tower(),
          mech2: nearFars,
        });
      },
      outputStrings: forsakenVoiceWords,
    },
    {
      id: 'DMU P2 Path of Light Towers 7',
      type: 'StartsUsing',
      netRegex: { id: ['BADC', 'BADD'], source: 'Kefka', capture: false },
      condition: (pull) => pull.pathOfLightCounter === 7,
      suppressSeconds: 1,
      alertText: (pull, _hit, voice) => {
        const num = pull.pathOfLightCounter;
        if (pull.triggerSetConfig.forsaken === 'buddy')
          return umadBuddyPut(pull, voice, num);
        const sign = pull.myPathOfLights.at(-1) ?? 'unknown';
        if (pull.isForsakenGroupA) {
          if (pull.triggerSetConfig.forsaken === 'kroxy-rinon') {
            return voice.markerOnYouTower({
              num: voice.num({ num: num }),
              marker: voice[sign](),
              tower: sign === 'cone'
                ? voice.leftTower()
                : voice.rightTower(),
            });
          }
          return voice.markerOnYouTower({
            num: voice.num({ num: num }),
            marker: voice[sign](),
            tower: voice.tower(),
          });
        }
        if (sign === 'stack') {
          const members = pull.pathOfLightStackPlayers.map(
            (player) => {
              if (player === pull.me)
                return voice.you();
              return pull.party.member(player);
            },
          );
          const msgs = members?.join(', ');
          return voice.markerOnYouTower({
            num: voice.num({ num: num }),
            marker: voice.stacksOnPlayers({ players: msgs }),
            tower: voice.tower(),
          });
        }
      },
      outputStrings: forsakenVoiceWords,
    },
    {
      id: 'DMU P2 Path of Light Towers 8',
      type: 'Ability',
      netRegex: {
        id: ['BABF', 'BAC0', 'BAC1', 'BAC2'],
        source: 'Kefka',
        capture: false,
      },
      condition: (pull) => pull.pathOfLightCounter === 8,
      delaySeconds: 0.1,
      durationSeconds: 9,
      suppressSeconds: 9999,
      infoText: (pull, _hit, voice) => {
        const num = pull.pathOfLightCounter;
        if (pull.triggerSetConfig.forsaken === 'buddy')
          return umadBuddyPut(pull, voice, num);
        const sign = pull.myPathOfLights.at(-1) ?? 'unknown';
        if (pull.isForsakenGroupA) {
          if (sign === 'stack' || sign === 'unknown')
            return;
          const nearFars = sign === 'spread'
            ? voice.beFar()
            : voice.beNear();
          if (pull.triggerSetConfig.forsaken === 'kroxy-rinon') {
            return voice.mechs({
              num: voice.num({ num: num }),
              mech1: pull.role === 'tank' || umadIsMeleeDp(pull)
                ? voice.rightTower()
                : voice.leftTower(),
              mech2: nearFars,
            });
          }
          return voice.mechs({
            num: voice.num({ num: num }),
            mech1: voice.tower(),
            mech2: nearFars,
          });
        }
        if (pull.role === 'healer')
          return voice.baitLeftConeEvens({
            num: voice.num({ num: num }),
          });
        if (pull.role === 'tank')
          return voice.baitCloneFar({
            num: voice.num({ num: num }),
          });
        return voice.bait({
          num: voice.num({ num: num }),
        });
      },
      outputStrings: forsakenVoiceWords,
    },
    {
      id: 'DMU P2 Light of Judgment',
      type: 'StartsUsing',
      netRegex: { id: 'BABD', source: 'Kefka', capture: false },
      response: Response.bigAoe('alert'),
    },
    {
      id: 'DMU P2 Trine Collector',
      type: 'CombatantMemory',
      netRegex: { change: 'Add', pair: [{ key: 'BNpcID', value: ['1EBFB2', '1EBFB3'] }], capture: true },
      run: (pull, hit) => {
        const x = parseFloat(hit.pairPosX ?? '0');
        const y = parseFloat(hit.pairPosY ?? '0');
        if (pull.trineDirNums.length === 3) {
          if (x > 99 && x < 101) {
            pull.middleTrineFacing = hit.pairBNpcID === '1EBFB2' ? 'west' : 'east';
            return;
          }
        }
        if (pull.trineDirNums.length !== 3) {
          const facingDigit = Facings.xyTo16DirNum(x, y, centerXBit, centerYBit);
          pull.trineDirNums.push(facingDigit);
        }
      },
    },
    {
      id: 'DMU P2 Trines 1 (Early)',
      type: 'CombatantMemory',
      netRegex: { change: 'Add', pair: [{ key: 'BNpcID', value: ['1EBFB2', '1EBFB3'] }], capture: false },
      condition: (pull) => pull.trineDirNums.length === 3,
      durationSeconds: 12,
      suppressSeconds: 99999,
      infoText: (pull, _hit, voice) => {
        const sorteds = pull.trineDirNums.slice().sort((a, b) => a - b);
        const trine1s = sorteds[0] !== undefined ? Facings.output16Dir[sorteds[0]] ?? 'unknown' : 'unknown';
        const trine2s = sorteds[1] !== undefined ? Facings.output16Dir[sorteds[1]] ?? 'unknown' : 'unknown';
        const trine3s = sorteds[2] !== undefined ? Facings.output16Dir[sorteds[2]] ?? 'unknown' : 'unknown';
        const crewFacing = trine1s;
        const tankFacing = trine3s;
        const minTwo = pull.role === 'tank' ? tankFacing : crewFacing;
        return voice.mySpot({
          dir: voice[minTwo](),
          dirs: voice.safeSpots({ dir1: voice[trine1s](), dir2: voice[trine2s](), dir3: voice[trine3s]() }),
        });
      },
      outputStrings: Object.assign({}, Facings.outputStrings16Dir, {
        unknown: Voices.unknown,
        safeSpots: { en: '${dir1}/${dir2}/${dir3}' },
        mySpot: { en: '${dir} Later (${dirs})' },
      }),
    },
    {
      id: 'DMU P2 Single Wing of Destruction',
      type: 'StartsUsing',
      netRegex: { id: ['BACD', 'BACE'], source: 'Kefka', capture: true },
      infoText: (_pull, hit, voice) => {
        if (hit.id === 'BACD')
          return voice.right();
        return voice.left();
      },
      outputStrings: {
        right: Voices.right,
        left: Voices.left,
      },
    },
    {
      id: 'DMU P2 Wings of Destruction',
      type: 'StartsUsing',
      netRegex: { id: 'C487', source: 'Kefka', capture: false },
      alertText: (pull, _hit, voice) => {
        const sorteds = pull.trineDirNums.slice().sort((a, b) => a - b);
        const trine1s = sorteds[0] !== undefined ? Facings.output16Dir[sorteds[0]] ?? 'unknown' : 'unknown';
        const trine2s = sorteds[1] !== undefined ? Facings.output16Dir[sorteds[1]] ?? 'unknown' : 'unknown';
        const trine3s = sorteds[2] !== undefined ? Facings.output16Dir[sorteds[2]] ?? 'unknown' : 'unknown';
        const wing = pull.role !== 'tank'
          ? voice.wingsParty()
          : pull.middleTrineFacing
            ? voice.wingsTrine({ wings: voice.wingsTank(), trine: voice[pull.middleTrineFacing]() })
            : voice.wingsTank();
        return voice.dirWings({
          dirs: voice.safeSpots({ dir1: voice[trine1s](), dir2: voice[trine2s](), dir3: voice[trine3s]() }),
          wings: wing,
        });
      },
      outputStrings: Object.assign({}, Facings.outputStrings16Dir, {
        unknown: Voices.unknown,
        safeSpots: { en: '${dir1}/${dir2}/${dir3}' },
        wingsTrine: { en: '${wings} + ${trine}' },
        dirWings: { en: '${dirs} + ${wings}' },
        wingsParty: { en: 'Outer 2 Rings' },
        wingsTank: { en: 'Be Near/Far' },
        east: { en: 'Eastward Trine' },
        west: { en: 'Westward Trine' },
      }),
    },
    {
      id: 'DMU P2 Aero III Assault',
      type: 'StartsUsing',
      netRegex: { id: 'C3F7', source: 'Kefka', capture: false },
      response: Response.getUnder('alert'),
    },
    {
      id: 'DMU P3 Epic Hero/Fated Hero Debuffs',
      type: 'GainsEffect',
      netRegex: { effectId: ['1060', '1062'], capture: true },
      condition: Condition.targetIsYou(),
      infoText: (_pull, hit, voice) => {
        return hit.effectId === '1060' ? voice.epic() : voice.fated();
      },
      outputStrings: {
        epic: { en: 'Attack Chaos', ko: '\uCE74\uC624\uC2A4 \uACF5\uACA9' },
        fated: { en: 'Attack Exdeath', ko: '\uC5D1\uC2A4\uB370\uC2A4 \uACF5\uACA9' },
      },
    },
    {
      id: 'DMU P3 Bowels of Agony',
      type: 'StartsUsing',
      netRegex: { id: 'BAF2', source: 'Chaos', capture: false },
      response: Response.aoe(),
    },
    {
      id: 'DMU P3 Entropy and Dynamic Fluid Debuff Collector',
      type: 'GainsEffect',
      netRegex: { effectId: ['640', '641'], capture: true },
      run: (pull, hit) => {
        const id = hit.effectId;
        if (pull.isFireShort === undefined) {
          const isShorts = parseFloat(hit.duration) < 20;
          pull.isFireShort = (isShorts && id === '640') || (!isShorts && id === '641') ? true : false;
        }
        if (pull.me === hit.target)
          pull.myElement = id === '640' ? 'fire' : 'water';
        if (id === '640')
          pull.fireElementPlayers.push(hit.target);
        else
          pull.waterElementPlayers.push(hit.target);
      },
    },
    {
      id: 'DMU P3 Headwind/Tailwind Debuff Collector',
      type: 'GainsEffect',
      netRegex: { effectId: ['642', '643'], capture: true },
      condition: Condition.targetIsYou(),
      run: (pull, hit) => pull.myWind = hit.effectId === '642' ? 'head' : 'tail',
    },
    {
      id: 'DMU P3 Headwind/Tailwind Debuff',
      type: 'GainsEffect',
      netRegex: { effectId: ['642', '643'], capture: true },
      condition: Condition.targetIsYou(),
      delaySeconds: 0.1,
      infoText: (pull, hit, voice) => {
        const myElements = pull.myElement;
        const shorts = pull.isFireShort ? voice.shortFire() : voice.shortWater();
        const winds = hit.effectId === '642' ? voice.headwind() : voice.tailwind();
        if (myElements !== undefined)
          return voice.withElement({ short: shorts, element: voice[myElements](), wind: winds });
        return voice.withoutElement({ short: shorts, wind: winds });
      },
      outputStrings: {
        shortFire: { en: 'Short Fire' },
        shortWater: { en: 'Short Water' },
        fire: { en: 'Fire' },
        water: { en: 'Water' },
        headwind: { en: 'Headwind on YOU' },
        tailwind: { en: 'Tailwind on YOU' },
        withElement: { en: '${short}: ${element} + ${wind}' },
        withoutElement: { en: '${short}: ${wind}' },
      },
    },
    {
      id: 'DMU P3 Crystal Location Collector',
      type: 'CombatantMemory',
      netRegex: { change: 'Add', pair: [{ key: 'BNpcID', value: ['1EC03A', '1EC03B', '1EC03C'] }], capture: true },
      run: (pull, hit) => {
        const x = parseFloat(hit.pairPosX ?? '0');
        const y = parseFloat(hit.pairPosY ?? '0');
        const bnpcids = hit.pairBNpcID;
        const facingTwo = Facings.xyToIntercardDirOutput(x, y, centerXBit, centerYBit);
        if (bnpcids === '1EC03A') pull.fireCrystalDir = facingTwo;
        else if (bnpcids === '1EC03B') pull.waterCrystalDir = facingTwo;
        else pull.windCrystalDir = facingTwo;
      },
    },
    {
      id: 'DMU P3 Short Crystal and Crystal Locations',
      type: 'CombatantMemory',
      netRegex: { change: 'Add', pair: [{ key: 'BNpcID', value: '1EC03C' }], capture: false },
      delaySeconds: 2,
      durationSeconds: 17,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const fireFacing = pull.fireCrystalDir ?? 'unknown';
        const waterFacing = pull.waterCrystalDir ?? 'unknown';
        const windFacing = pull.windCrystalDir ?? 'unknown';
        const fShorts = pull.isFireShort;
        const fir = voice.fire({ dir: voice[fireFacing]() });
        const waters = voice.water({ dir: voice[waterFacing]() });
        return voice.crystals({
          short: fShorts ? fir : waters,
          long: fShorts ? waters : fir,
          wind: voice.wind({ dir: voice[windFacing]() }),
        });
      },
      outputStrings: Object.assign({}, Facings.outputStringsIntercardDir, {
        unknown: Voices.unknown,
        fire: { en: 'Fire ${dir}' },
        water: { en: 'Water ${dir}' },
        wind: { en: 'Wind ${dir}' },
        crystals: { en: '${short} => ${long} => ${wind} (later)' },
      }),
    },
    {
      id: 'DMU P3 Entropy and Fire Crystal',
      type: 'GainsEffect',
      netRegex: { effectId: '640', capture: true },
      delaySeconds: (_pull, hit) => parseFloat(hit.duration) - 5,
      suppressSeconds: 1,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = Object.assign({}, Facings.outputStringsIntercardDir, {
          you: { en: 'YOU' },
          bait: { en: 'Bait Fire Donut' },
          fireOnPlayersCrystal: { en: '${spread}/${bait}' },
          fireOnPlayersCrystalDir: { en: '${spread}/${dir} => ${bait}' },
          fireOnPlayers: { en: 'Spread on ${players}' },
        });
        const severities = pull.myElement === 'fire' ? 'alertText' : 'infoText';
        const members = pull.fireElementPlayers.map((player) => {
          if (player === pull.me) return voice.you();
          return pull.party.member(player);
        });
        const msgs = members.join(', ');
        const scatter = voice.fireOnPlayers({ players: msgs });
        if (pull.role === 'tank' || umadIsMeleeDpsCraft(pull.party.member(pull.me)))
          return { [severities]: scatter };
        const facingTwo = pull.fireCrystalDir;
        if (facingTwo === undefined)
          return { [severities]: voice.fireOnPlayersCrystal({ spread: scatter, bait: voice.bait() }) };
        return { [severities]: voice.fireOnPlayersCrystalDir({ spread: scatter, dir: voice[facingTwo](), bait: voice.bait() }) };
      },
    },
    {
      id: 'DMU P3 Dynamic Fluid and Water Crystal',
      type: 'GainsEffect',
      netRegex: { effectId: '641', capture: true },
      delaySeconds: (_pull, hit) => parseFloat(hit.duration) - 5,
      suppressSeconds: 1,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = Object.assign({}, Facings.outputStringsIntercardDir, {
          you: { en: 'YOU' },
          bait: { en: 'Bait Water AOE' },
          waterOnPlayersCrystal: { en: '${donut}/${bait}' },
          waterOnPlayersCrystalDir: { en: '${donut}/${dir} => ${bait}' },
          waterOnPlayers: { en: 'Donut on ${players}' },
        });
        const severities = pull.myElement === 'water' ? 'alertText' : 'infoText';
        const members = pull.waterElementPlayers.map((player) => {
          if (player === pull.me) return voice.you();
          return pull.party.member(player);
        });
        const msgs = members.join(', ');
        const donutsTwo = voice.waterOnPlayers({ players: msgs });
        if (pull.role === 'tank' || umadIsMeleeDpsCraft(pull.party.member(pull.me)))
          return { [severities]: donutsTwo };
        const facingTwo = pull.waterCrystalDir;
        if (facingTwo === undefined)
          return { [severities]: voice.waterOnPlayersCrystal({ donut: donutsTwo, bait: voice.bait() }) };
        return { [severities]: voice.waterOnPlayersCrystalDir({ donut: donutsTwo, dir: voice[facingTwo](), bait: voice.bait() }) };
      },
    },
    {
      id: 'DMU P3 Long Crystal and Wind Crystal Locations',
      type: 'Ability',
      netRegex: { id: ['BAF3', 'BAF6'], source: 'Chaos', capture: true },
      condition: (pull, hit) => {
        const fShorts = pull.isFireShort;
        const id = hit.id;
        return (fShorts && id === 'BAF3') || (!fShorts && id === 'BAF6');
      },
      durationSeconds: 27,
      suppressSeconds: 99999,
      infoText: (pull, _hit, voice) => {
        const fShorts = pull.isFireShort;
        const longCrystalFacing = fShorts ? pull.waterCrystalDir : pull.fireCrystalDir;
        const longFacing = longCrystalFacing ?? 'unknown';
        const windFacing = pull.windCrystalDir ?? 'unknown';
        return voice.crystals({
          long: fShorts ? voice.fire({ dir: voice[longFacing]() }) : voice.water({ dir: voice[longFacing]() }),
          wind: voice.wind({ dir: voice[windFacing]() }),
        });
      },
      outputStrings: Object.assign({}, Facings.outputStringsIntercardDir, {
        unknown: Voices.unknown,
        fire: { en: 'Fire ${dir}' },
        water: { en: 'Water ${dir}' },
        wind: { en: 'Wind ${dir}' },
        crystals: { en: '${long} => ${wind} (later)' },
      }),
    },
    {
      id: 'DMU P3 Wind Crystal Next Flag',
      type: 'Ability',
      netRegex: { id: 'BAFF', source: 'Chaos', capture: false },
      suppressSeconds: 99999,
      run: (pull) => pull.windCrystalNext = true,
    },
    {
      id: 'DMU P3 Wind Crystal Location',
      type: 'Ability',
      netRegex: { id: ['BAF3', 'BAF6'], source: 'Chaos', capture: false },
      condition: (pull) => pull.windCrystalNext,
      suppressSeconds: 99999,
      infoText: (pull, _hit, voice) => {
        const windFacing = pull.windCrystalDir ?? 'unknown';
        return voice.wind({ dir: voice[windFacing]() });
      },
      outputStrings: Object.assign({}, Facings.outputStringsIntercardDir, {
        unknown: Voices.unknown,
        wind: { en: 'Knockback to Wind ${dir} (later)' },
      }),
    },
    {
      id: 'DMU P3 Headwind/Tailwind Cleanup',
      type: 'LosesEffect',
      netRegex: { effectId: ['642', '643'], capture: true },
      condition: Condition.targetIsYou(),
      run: (pull) => delete pull.myWind,
    },
    {
      id: 'DMU P3 Thunder III AOE',
      type: 'StartsUsing',
      netRegex: { id: 'BB12', source: 'Exdeath', capture: true },
      durationSeconds: (_pull, hit) => parseFloat(hit.castTime),
      infoText: (_pull, hit, voice) => voice.awayFromBoss({ boss: hit.source }),
      outputStrings: {
        awayFromBoss: { en: 'Away from ${boss}' },
      },
    },
    {
      id: 'DMU P3 Thunder III Tankbuster',
      type: 'StartsUsing',
      netRegex: { id: 'BB09', source: 'Exdeath', capture: true },
      response: (pull, hit, voice) => {
        voice.responseOutputStrings = {
          avoid: { en: '${boss}${cleaves}' },
          tankCleaveNearThenSwap: { en: 'Near ${boss}${cleave} => ${swap}' },
          boss: { en: '${boss}: ' },
          tankCleave: Voices.tankCleave,
          avoidTankCleaves: Voices.avoidTankCleaves,
          tankSwap: Voices.tankSwap,
        };
        const severities = pull.role === 'tank' || pull.role === 'healer' ? 'alertText' : 'infoText';
        const bigTwo = voice.boss({ boss: hit.source });
        if (pull.role === 'tank')
          return { [severities]: voice.tankCleaveNearThenSwap({ boss: bigTwo, cleave: voice.tankCleave(), swap: voice.tankSwap() }) };
        return { [severities]: voice.avoid({ boss: bigTwo, cleaves: voice.avoidTankCleaves() }) };
      },
    },
    {
      id: 'DMU P3 Thunder III Tank Swap',
      type: 'Ability',
      netRegex: { id: 'BB09', source: 'Exdeath', capture: true },
      condition: (pull) => pull.role === 'tank',
      suppressSeconds: 4,
      alertText: (pull, hit, voice) => {
        const bigTwo = hit.source;
        if (hit.target === pull.me) return voice.awayFromBoss({ boss: bigTwo });
        return voice.beNearBoss({ boss: bigTwo });
      },
      outputStrings: {
        beNearBoss: { en: 'Be Near ${boss} (swap)' },
        awayFromBoss: { en: 'Away from ${boss} (swap)' },
      },
    },
    {
      id: 'DMU P3 Ultima Blaster Collect',
      type: 'Ability',
      netRegex: { id: 'BAE3', source: 'Kefka', capture: true },
      condition: (pull, hit) => {
        const x2Bit = parseFloat(hit.x);
        const y2Bit = parseFloat(hit.y);
        if (isNaN(x2Bit) || isNaN(y2Bit)) return false;
        if (pull.firstBlaster === undefined) {
          pull.firstBlaster = [x2Bit, y2Bit];
          pull.firstBlasterDirNum = (Facings.xyTo8DirNum(x2Bit, y2Bit, centerXBit, centerYBit) + 4) % 8;
          return false;
        }
        const x1Bit = pull.firstBlaster[0];
        const y1Bit = pull.firstBlaster[1];
        if (x1Bit === undefined || y1Bit === undefined) {
          pull.firstBlaster = [x2Bit, y2Bit];
          return false;
        }
        pull.blasterRotation = Math.atan2(y1Bit * x2Bit - x1Bit * y2Bit, y1Bit * y2Bit + x1Bit * x2Bit);
        return true;
      },
      suppressSeconds: 99999,
    },
    {
      id: 'DMU P3 Ultima Blaster Rotation',
      type: 'Ability',
      netRegex: { id: 'BAE3', source: 'Kefka', capture: false },
      condition: (pull) => pull.blasterRotation !== undefined,
      durationSeconds: 10,
      suppressSeconds: 99999,
      infoText: (pull, _hit, voice) => {
        const rotation = pull.blasterRotation;
        const facingDigit = pull.firstBlasterDirNum;
        if (rotation === undefined || facingDigit === undefined) return;
        const facingTwo = Facings.output8Dir[facingDigit] ?? 'unknown';
        if (rotation < 0) return voice.clockwise({ card: voice[facingTwo]() });
        if (rotation > 0) return voice.counterclockwise({ card: voice[facingTwo]() });
      },
      outputStrings: Object.assign({}, Facings.outputStrings8Dir, {
        unknown: Voices.unknown,
        clockwise: { en: '<== ${card} Clockwise (Later)' },
        counterclockwise: { en: '${card} Counterclockwise (Later) ==>' },
      }),
    },
    {
      id: 'DMU P3 Umbra Smash',
      type: 'Ability',
      netRegex: { id: ['BAFD', 'BAFE'], source: 'Chaos', capture: false },
      delaySeconds: 10,
      suppressSeconds: 99999,
      infoText: (_pull, _hit, voice) => voice.bait(),
      outputStrings: {
        bait: { en: 'Bait Jump' },
      },
    },
    {
      id: 'DMU P3 Vacuum Wave',
      type: 'StartsUsing',
      netRegex: { id: 'BB13', source: 'Exdeath', capture: true },
      infoText: (pull, hit, voice) => {
        const chaosLabel = 'Chaos';
        const windFacing = pull.windCrystalDir;
        const knockbacks = voice.knockbackFromChaos({ chaos: chaosLabel });
        const exdeaths = hit.source;
        if (pull.myWind === undefined) {
          if (windFacing === undefined) return voice.knockbackFromChaosToCrystal({ knockback: knockbacks });
          return voice.knockbackFromChaosToDir({ knockback: knockbacks, dir: voice[windFacing]() });
        }
        if (windFacing === undefined)
          return voice.knockbackFromChaosToWindFacing({
            knockbackDir: voice.knockbackFromChaosToCrystal({ knockback: knockbacks }),
            facing: voice[pull.myWind]({ target: exdeaths }),
          });
        return voice.knockbackFromChaosToWindFacing({
          knockbackDir: voice.knockbackFromChaosToDir({ knockback: knockbacks, dir: voice[windFacing]() }),
          facing: voice[pull.myWind]({ target: exdeaths }),
        });
      },
      outputStrings: Object.assign({}, Facings.outputStringsIntercardDir, {
        head: { en: 'Look Away from ${target}' },
        tail: { en: 'Face ${target}' },
        knockbackFromChaos: { en: 'Knockback from ${chaos}' },
        knockbackFromChaosToDir: { en: '${knockback} to ${dir}' },
        knockbackFromChaosToCrystal: { en: '${knockback} to Crystal' },
        knockbackFromChaosToWindFacing: { en: '${knockbackDir} + ${facing}' },
      }),
    },
    {
      id: 'DMU P3 Ultima Blaster Location',
      type: 'HeadMarker',
      netRegex: {
        id: [
          headSignState['1'], headSignState['2'], headSignState['3'], headSignState['4'],
          headSignState['5'], headSignState['6'], headSignState['7'], headSignState['8'],
        ],
        capture: true,
      },
      condition: Condition.targetIsYou(),
      infoText: (pull, hit, voice) => {
        const capSliceDigitTable = {
          '004F': 1, '0050': 2, '0051': 3, '0052': 4,
          '0053': 5, '0054': 6, '0055': 7, '0056': 8,
        };
        const blasters = pull.firstBlasterDirNum;
        const rotation = pull.blasterRotation;
        const id = hit.id;
        const myDigit = capSliceDigitTable[id];
        if (myDigit === undefined) return;
        if (blasters === undefined || rotation === undefined || rotation === 0)
          return voice.num({ num: myDigit });
        const blaster16Facing = blasters * 2;
        const adjustedFacingDigit = rotation < 0
          ? (myDigit + blaster16Facing) % 16
          : (myDigit - blaster16Facing + 16) % 16;
        const clearFacing = Facings.output16Dir[adjustedFacingDigit] ?? 'unknown';
        return voice.text({ num: voice.num({ num: myDigit }), dir: voice[clearFacing]() });
      },
      outputStrings: Object.assign({}, Facings.outputStrings16Dir, {
        unknown: Voices.unknown,
        num: { en: '#${num}' },
        text: { en: '${num}: ${dir}' },
      }),
    },
    {
      id: 'DMU P3 In Line Debuff Collector',
      type: 'GainsEffect',
      netRegex: { effectId: ['BBC', 'BBD', 'BBE'], capture: true },
      run: (pull, hit) => {
        const auraToDigit = { BBC: 1, BBD: 2, BBE: 3 };
        const num = auraToDigit[hit.effectId];
        if (num === undefined) return;
        pull.inLine[hit.target] = num;
      },
    },
    {
      id: 'DMU P3 Accretion Collector',
      type: 'GainsEffect',
      netRegex: { effectId: '644', capture: true },
      delaySeconds: 0.1,
      run: (pull, hit) => {
        const markThree = hit.target;
        if (pull.inLine[markThree] === 1) pull.firstAccretion = markThree;
        else pull.secondAccretion = markThree;
        if (pull.me === markThree) pull.hadAccretion = true;
      },
    },
    {
      id: 'DMU P3 In Line Debuff',
      type: 'GainsEffect',
      netRegex: { effectId: ['BBC', 'BBD', 'BBE'], capture: false },
      delaySeconds: 0.1,
      durationSeconds: 5,
      suppressSeconds: 1,
      infoText: (pull, _hit, voice) => {
        const myDigit = pull.inLine[pull.me];
        if (myDigit === undefined) return;
        if (pull.role === 'healer') {
          const lead = pull.firstAccretion;
          const seconds = pull.secondAccretion;
          const player1s = lead === pull.me ? voice.you() : pull.party.member(lead);
          const player2s = seconds === pull.me ? voice.you() : pull.party.member(seconds);
          return voice.accretionHealer({ num: myDigit, player1: player1s, player2: player2s });
        }
        const partner = [];
        const entrie = Object.entries(pull.inLine);
        for (let i = 0; i < entrie.length; i++) {
          const labelTwo = entrie[i][0];
          const num = entrie[i][1];
          if (num === myDigit && labelTwo !== pull.me) partner.push(pull.party.member(labelTwo));
        }
        const msgs = partner.join(', ');
        return voice.text({ num: myDigit, players: msgs });
      },
      outputStrings: {
        you: { en: 'YOU' },
        text: { en: '${num} (with ${players})' },
        accretionHealer: { en: '${num}: Accretion on ${player1} => ${player2}' },
      },
    },
    {
      id: 'DMU P3 Accretion Cleanup',
      type: 'LosesEffect',
      netRegex: { effectId: '644', capture: true },
      run: (pull, hit) => {
        const markThree = hit.target;
        if (markThree === pull.firstAccretion) delete pull.firstAccretion;
        else delete pull.secondAccretion;
      },
    },
    {
      id: 'DMU P3 Accretion 2',
      type: 'GainsEffect',
      netRegex: { effectId: 'D2C', capture: true },
      condition: (pull) => pull.firstAccretion !== undefined || pull.secondAccretion !== undefined,
      delaySeconds: (_pull, hit) => parseFloat(hit.duration),
      suppressSeconds: 1,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          healPlayerFull: { en: 'Heal ${player} to full' },
        };
        const player = pull.firstAccretion !== undefined ? pull.firstAccretion : pull.secondAccretion;
        const severities = pull.role === 'healer' ? 'alertText' : 'infoText';
        return { [severities]: voice.healPlayerFull({ player: pull.party.member(player) }) };
      },
    },
    {
      id: 'DMU P3 Slap Happy',
      type: 'StartsUsing',
      netRegex: { id: ['BAE6', 'BAE7'], source: 'Kefka', capture: true },
      alertText: (_pull, hit, voice) => {
        const id = hit.id;
        const x = parseFloat(hit.x);
        const y = parseFloat(hit.y);
        const bigFacingDigit = Facings.xyTo8DirNum(x, y, centerXBit, centerYBit);
        const clockFacingDigit = (bigFacingDigit + 2) % 8;
        const counterFacingDigit = (bigFacingDigit + 6) % 8;
        const clockFacing = Facings.output8Dir[clockFacingDigit] ?? 'unknown';
        const counterFacing = Facings.output8Dir[counterFacingDigit] ?? 'unknown';
        const isStarboardSlap = id === 'BAE6';
        const facingTwo = isStarboardSlap ? clockFacing : counterFacing;
        return voice.slapDirMechThenOut({
          dir1: voice[facingTwo](),
          mech: isStarboardSlap ? voice.partyStack() : voice.roleStacks(),
          out: voice.outOfMiddle(),
        });
      },
      outputStrings: Object.assign({}, Facings.outputStrings8Dir, {
        unknown: Voices.unknown,
        outOfMiddle: { en: 'Out Of Middle' },
        partyStack: { en: 'Party Stack' },
        roleStacks: { en: 'Role Stacks' },
        slapDirMechThenOut: { en: '${dir1} + ${mech} => ${out}' },
      }),
    },
    {
      id: 'DMU P3 Black Hole Tracker',
      type: 'AddedCombatant',
      netRegex: { name: 'Black Hole', capture: true },
      run: (pull, hit) => {
        const x = parseFloat(hit.x);
        const y = parseFloat(hit.y);
        const facingDigit = Facings.xyTo4DirNum(x, y, centerXBit, centerYBit);
        pull.blackHoleIdDirNums[hit.id] = facingDigit;
      },
    },
    {
      id: 'DMU P3 Nothingness Counter',
      type: 'Ability',
      netRegex: { id: 'BAFC', source: 'Black Hole', capture: false },
      suppressSeconds: 1,
      run: (pull) => {
        pull.nothingnessTracker = pull.nothingnessTracker + 1;
        if (
          pull.nothingnessTracker === 2 || pull.nothingnessTracker === 3 ||
          pull.nothingnessTracker === 6 || pull.nothingnessTracker === 9 ||
          pull.nothingnessTracker === 10
        ) {
          delete pull.blackHoleTetherDisable;
          pull.blackHoleTetherDirNums = [];
        }
      },
    },
    {
      id: 'DMU P3 Black Hole Tether Collect (NetworkTether)',
      type: 'Tether',
      netRegex: { id: headSignState['blackHoleTether'], capture: true },
      condition: (pull) => {
        return pull.nothingnessTracker !== 1 && pull.nothingnessTracker !== 10;
      },
      run: (pull, hit) => {
        const facingDigit = pull.blackHoleIdDirNums[hit.sourceId];
        if (facingDigit === undefined)
          return;
        if (!pull.blackHoleTetherDirNums.includes(facingDigit))
          pull.blackHoleTetherDirNums.push(facingDigit);
      },
    },
    {
      id: 'DMU P3 Black Hole Tether Collect (SpawnNpcExtra)',
      type: 'SpawnNpcExtra',
      netRegex: { tetherId: headSignState['blackHoleTether'], capture: true },
      delaySeconds: 0.1,
      run: (pull, hit) => {
        const facingDigit = pull.blackHoleIdDirNums[hit.id];
        if (facingDigit === undefined)
          return;
        if (!pull.blackHoleTetherDirNums.includes(facingDigit))
          pull.blackHoleTetherDirNums.push(facingDigit);
      },
    },
    {
      id: 'DMU P3 Black Hole 1, Nothingness 1',
      type: 'SpawnNpcExtra',
      netRegex: { tetherId: headSignState['blackHoleTether'], capture: true },
      condition: (pull) => pull.nothingnessTracker === 1,
      delaySeconds: 0.1,
      suppressSeconds: 99999,
      response: (pull, hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const num = voice.num({ num: pull.nothingnessTracker });
        const kefkaFacing = pull.kefkaTeleportDirNum;
        const facingDigit = pull.blackHoleIdDirNums[hit.id];
        const facingTwo = facingDigit === undefined
          ? 'unknown'
          : Facings.outputFromCardinalNum(facingDigit);

        if (setup !== 'none') {
          const dutyTwo = setup === 'sda' || setup === 'modified'
            ? pull.role !== 'dps'
            : pull.role === 'dps';
          const relFacing = relSetup === 'true' ? facingTwo : 'clockwiseOne';
          if (pull.inLine[pull.me] === 1 && !pull.hadAccretion && dutyTwo)
            return {
              alertText: voice.getDirTether({
                num: num,
                dir: voice[relFacing](),
              }),
            };

          if (pull.inLine[pull.me] === 1 && !pull.hadAccretion && !dutyTwo) {
            const facingNum1 = facingDigit === undefined ? undefined : (facingDigit + 2) % 4;
            const facingNum2 = facingDigit === undefined ? undefined : (facingDigit + 3) % 4;

            if (facingNum1 === undefined || facingNum2 === undefined) {
              if (setup === 'modified')
                return {
                  infoText: voice.middleThenGetBothTethers({ num: num }),
                };
              const afterFacing = pull.role === 'dps'
                ? 'clockwiseOne'
                : 'clockwiseTwo';
              return {
                infoText: voice.middleThenGetDirTether({
                  num: num,
                  dir: voice[afterFacing](),
                }),
              };
            }
            const facingNums = [facingNum1, facingNum2];
            const openFacing = kefkaFacing !== undefined
              ? Math.round(kefkaFacing / 2) % 4
              : -1;
            const sorteds = openFacing !== -1 ? readCWSequenceFromN(openFacing, facingNums) : [];
            const dir1s = sorteds[0] !== undefined
              ? Facings.outputFromCardinalNum(sorteds[0])
              : 'unknown';
            const dir2s = sorteds[1] !== undefined
              ? Facings.outputFromCardinalNum(sorteds[1])
              : 'unknown';

            if (setup === 'modified') {
              return {
                infoText: relSetup === 'true'
                  ? voice.middleThenGetDirTethers({
                    num: num,
                    dir1: voice[dir1s](),
                    dir2: voice[dir2s](),
                  })
                  : voice.middleThenGetBothTethers({ num: num }),
              };
            }

            const minTwo = pull.role === 'dps' ? dir1s : dir2s;
            const mineRels = relSetup === 'true'
              ? minTwo
              : pull.role === 'dps'
              ? 'clockwiseOne'
              : 'clockwiseTwo';

            return {
              infoText: voice.middleThenGetDirTether({
                num: num,
                dir: voice[mineRels](),
              }),
            };
          }
        }
        return {
          infoText: voice.oneBlackHole({
            num: num,
            dir: voice[facingTwo](),
          }),
        };
      },
    },
    {
      id: 'DMU P3 Black Hole 2, Nothingness 2',
      type: 'Tether',
      netRegex: { id: headSignState['blackHoleTether'], capture: false },
      condition: (pull) => {
        return pull.nothingnessTracker === 2 && pull.blackHoleTetherDirNums.length === 2;
      },
      suppressSeconds: 99999,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const num = voice.num({ num: pull.nothingnessTracker });
        const kefkaFacing = pull.kefkaTeleportDirNum;
        const facingNums = pull.blackHoleTetherDirNums;

        const openFacing = kefkaFacing !== undefined
          ? Math.round(kefkaFacing / 2) % 4
          : -1;
        const sorteds = openFacing !== -1 ? readCWSequenceFromN(openFacing, facingNums) : [];
        const dir1s = sorteds[0] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[0])
          : 'unknown';
        const dir2s = sorteds[1] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[1])
          : 'unknown';

        if (setup === 'dsa' || setup === 'sda') {
          const facingTwo = pull.role === 'dps' ? dir1s : dir2s;
          const relFacing = relSetup === 'true'
            ? facingTwo
            : pull.role === 'dps'
            ? 'clockwiseOne'
            : 'clockwiseTwo';
          if (pull.inLine[pull.me] === 1 && !pull.hadAccretion) {
            return {
              alertText: voice.getDirTether({
                num: num,
                dir: voice[relFacing](),
              }),
            };
          }
        } else if (
          setup === 'modified' && pull.inLine[pull.me] === 1 &&
          !pull.hadAccretion && pull.role === 'dps'
        ) {
          return {
            alertText: relSetup === 'true'
              ? voice.getDirTethers({
                num: num,
                dir1: voice[dir1s](),
                dir2: voice[dir2s](),
              })
              : voice.getBothTethers({
                num: num,
              }),
          };
        }

        return {
          infoText: voice.twoBlackHoles({
            num: num,
            dir1: voice[dir1s](),
            dir2: voice[dir2s](),
          }),
        };
      },
    },
    {
      id: 'DMU P3 Black Hole 3, Nothingness 3',
      type: 'SpawnNpcExtra',
      netRegex: { tetherId: headSignState['blackHoleTether'], capture: false },
      condition: (pull) => pull.nothingnessTracker === 3,
      delaySeconds: 0.1,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        if (pull.blackHoleTetherDirNums.length !== 3 || pull.blackHoleTetherDisable)
          return;
        pull.blackHoleTetherDisable = true;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const num = voice.num({ num: pull.nothingnessTracker });
        const hadAccretions = pull.hadAccretion;
        const lin = pull.inLine[pull.me];
        const kefkaFacing = pull.kefkaTeleportDirNum;
        const facingNums = pull.blackHoleTetherDirNums;

        const openFacing = kefkaFacing !== undefined
          ? Math.round(kefkaFacing / 2) % 4
          : -1;
        const sorteds = openFacing !== -1 ? readCWSequenceFromN(openFacing, facingNums) : [];
        const dir1s = sorteds[0] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[0])
          : 'unknown';
        const dir2s = sorteds[1] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[1])
          : 'unknown';
        const dir3s = sorteds[2] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[2])
          : 'unknown';

        if (setup !== 'none') {
          if (lin === 1) {
            if (hadAccretions) {
              const relFacing = relSetup === 'true' ? dir3s : 'clockwiseThree';
              return {
                alertText: voice.getDirTether({
                  num: num,
                  dir: voice[relFacing](),
                }),
              };
            }
            if (pull.role === 'dps') {
              const relFacing = relSetup === 'true' ? dir1s : 'clockwiseOne';
              return {
                alertText: voice.getDirTether({
                  num: num,
                  dir: voice[relFacing](),
                }),
              };
            }
            const relFacing = relSetup === 'true' ? dir2s : 'clockwiseTwo';
            return {
              alertText: voice.getDirTether({
                num: num,
                dir: voice[relFacing](),
              }),
            };
          }

          if (lin === 2 && !hadAccretions) {
            const dsaOrModifieds = setup === 'dsa' || setup === 'modified';
            const dutyTrade = dsaOrModifieds ? pull.role === 'dps' : pull.role !== 'dps';
            if (dutyTrade) {
              const sortedFacing = dsaOrModifieds ? sorteds[0] : sorteds[1];
              const afterTwo = sortedFacing !== undefined
                ? Facings.outputFromCardinalNum(sortedFacing)
                : 'unknown';
              const relFacing = relSetup === 'true'
                ? afterTwo
                : dsaOrModifieds
                ? 'clockwiseOne'
                : 'clockwiseTwo';
              return {
                infoText: voice.middleThenGetDirTether({
                  num: num,
                  dir: voice[relFacing](),
                }),
              };
            }
          }
        }

        return {
          infoText: voice.threeBlackHoles({
            num: num,
            dir1: voice[dir1s](),
            dir2: voice[dir2s](),
            dir3: voice[dir3s](),
          }),
        };
      },
    },
    {
      id: 'DMU P3 Black Hole 3, Nothingness 4',
      type: 'Ability',
      netRegex: { id: 'BAFC', source: 'Black Hole', capture: false },
      condition: (pull) => pull.nothingnessTracker === 4,
      suppressSeconds: 99999,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const trackers = pull.nothingnessTracker;
        const num = voice.num({ num: trackers });
        const hadAccretions = pull.hadAccretion;
        const lin = pull.inLine[pull.me];

        if (setup !== 'none') {
          const dsaOrModifieds = setup === 'dsa' || setup === 'modified';
          const dutyKeep = dsaOrModifieds ? pull.role !== 'dps' : pull.role === 'dps';
          if (lin === 1) {
            if (hadAccretions || dutyKeep)
              return { infoText: voice.keepTether({ num: num }) };
            return { alertText: voice.passTether({ num: num }) };
          }
          if (lin === 2 && !hadAccretions) {
            const dutyTrade = dsaOrModifieds ? pull.role === 'dps' : pull.role !== 'dps';
            const kefkaFacing = pull.kefkaTeleportDirNum;
            const facingNums = pull.blackHoleTetherDirNums;
            const openFacing = kefkaFacing !== undefined
              ? Math.round(kefkaFacing / 2) % 4
              : -1;
            const sorteds = openFacing !== -1 ? readCWSequenceFromN(openFacing, facingNums) : [];
            if (dutyTrade) {
              const sortedFacing = dsaOrModifieds ? sorteds[0] : sorteds[1];
              const afterTwo = sortedFacing !== undefined
                ? Facings.outputFromCardinalNum(sortedFacing)
                : 'unknown';
              const relFacing = relSetup === 'true'
                ? afterTwo
                : dsaOrModifieds
                ? 'clockwiseOne'
                : 'clockwiseTwo';
              return {
                alertText: voice.getDirTether({
                  num: num,
                  dir: voice[relFacing](),
                }),
              };
            }
            const sortedDir2s = dsaOrModifieds ? sorteds[1] : sorteds[0];
            const dir2s = sortedDir2s !== undefined
              ? Facings.outputFromCardinalNum(sortedDir2s)
              : 'unknown';
            const relFacing = relSetup === 'true'
              ? dir2s
              : dsaOrModifieds
              ? 'clockwiseTwo'
              : 'clockwiseOne';
            return {
              infoText: voice.middleThenGetDirTether({
                num: num,
                dir: voice[relFacing](),
              }),
            };
          }
        }

        return { infoText: voice.nothing({ num: trackers }) };
      },
    },
    {
      id: 'DMU P3 Black Hole 3, Nothingness 5',
      type: 'Ability',
      netRegex: { id: 'BAFC', source: 'Black Hole', capture: false },
      condition: (pull) => pull.nothingnessTracker === 5,
      suppressSeconds: 99999,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const trackers = pull.nothingnessTracker;
        const num = voice.num({ num: trackers });
        const hadAccretions = pull.hadAccretion;
        const lin = pull.inLine[pull.me];

        if (setup !== 'none') {
          const dsaOrModifieds = setup === 'dsa' || setup === 'modified';
          const dutyTrade = dsaOrModifieds ? pull.role !== 'dps' : pull.role === 'dps';
          if (lin === 1) {
            if (hadAccretions)
              return { infoText: voice.keepTether({ num: num }) };
            if (dutyTrade)
              return { alertText: voice.passTether({ num: num }) };
          }
          if (lin === 2 && !hadAccretions) {
            if (dutyTrade) {
              const kefkaFacing = pull.kefkaTeleportDirNum;
              const facingNums = pull.blackHoleTetherDirNums;
              const openFacing = kefkaFacing !== undefined
                ? Math.round(kefkaFacing / 2) % 4
                : -1;
              const sorteds = openFacing !== -1 ? readCWSequenceFromN(openFacing, facingNums) : [];
              const sortedFacing = dsaOrModifieds ? sorteds[1] : sorteds[0];
              const afterTwo = sortedFacing !== undefined
                ? Facings.outputFromCardinalNum(sortedFacing)
                : 'unknown';
              const relFacing = relSetup === 'true'
                ? afterTwo
                : dsaOrModifieds
                ? 'clockwiseTwo'
                : 'clockwiseOne';
              return {
                alertText: voice.getDirTether({
                  num: num,
                  dir: voice[relFacing](),
                }),
              };
            }
            return { infoText: voice.keepTether({ num: num }) };
          }
        }
        return { infoText: voice.nothing({ num: trackers }) };
      },
    },
    {
      id: 'DMU P3 Black Hole 4, Nothingness 6',
      type: 'SpawnNpcExtra',
      netRegex: { tetherId: headSignState['blackHoleTether'], capture: false },
      condition: (pull) => pull.nothingnessTracker === 6,
      delaySeconds: 0.1,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        if (pull.blackHoleTetherDirNums.length !== 3 || pull.blackHoleTetherDisable)
          return;
        pull.blackHoleTetherDisable = true;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const num = voice.num({ num: pull.nothingnessTracker });
        const hadAccretions = pull.hadAccretion;
        const lin = pull.inLine[pull.me];
        const kefkaFacing = pull.kefkaTeleportDirNum;
        const facingNums = pull.blackHoleTetherDirNums;

        const openFacing = kefkaFacing !== undefined
          ? Math.round(kefkaFacing / 2) % 4
          : -1;
        const sorteds = openFacing !== -1 ? readCWSequenceFromN(openFacing, facingNums) : [];
        const dir1s = sorteds[0] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[0])
          : 'unknown';
        const dir2s = sorteds[1] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[1])
          : 'unknown';
        const dir3s = sorteds[2] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[2])
          : 'unknown';

        if (setup !== 'none') {
          if (lin === 2) {
            if (hadAccretions) {
              const relFacing = relSetup === 'true' ? dir3s : 'clockwiseThree';
              return {
                alertText: voice.getDirTether({
                  num: num,
                  dir: voice[relFacing](),
                }),
              };
            }
            if (pull.role === 'dps') {
              const relFacing = relSetup === 'true' ? dir1s : 'clockwiseOne';
              return {
                alertText: voice.getDirTether({
                  num: num,
                  dir: voice[relFacing](),
                }),
              };
            }
            const relFacing = relSetup === 'true' ? dir2s : 'clockwiseTwo';
            return {
              alertText: voice.getDirTether({
                num: num,
                dir: voice[relFacing](),
              }),
            };
          }

          if (lin === 3) {
            const dsaOrModifieds = setup === 'dsa' || setup === 'modified';
            const dutyTrade = dsaOrModifieds ? pull.role === 'dps' : pull.role !== 'dps';
            if (dutyTrade) {
              const sortedFacing = dsaOrModifieds ? sorteds[0] : sorteds[1];
              const afterTwo = sortedFacing !== undefined
                ? Facings.outputFromCardinalNum(sortedFacing)
                : 'unknown';
              const relFacing = relSetup === 'true'
                ? afterTwo
                : dsaOrModifieds
                ? 'clockwiseOne'
                : 'clockwiseTwo';
              return {
                infoText: voice.middleThenGetDirTether({
                  num: num,
                  dir: voice[relFacing](),
                }),
              };
            }
          }
        }

        return {
          infoText: voice.threeBlackHoles({
            num: num,
            dir1: voice[dir1s](),
            dir2: voice[dir2s](),
            dir3: voice[dir3s](),
          }),
        };
      },
    },
    {
      id: 'DMU P3 Black Hole 4, Nothingness 7',
      type: 'Ability',
      netRegex: { id: 'BAFC', source: 'Black Hole', capture: false },
      condition: (pull) => pull.nothingnessTracker === 7,
      suppressSeconds: 99999,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const trackers = pull.nothingnessTracker;
        const num = voice.num({ num: trackers });
        const lin = pull.inLine[pull.me];

        if (setup !== 'none') {
          const dsaOrModifieds = setup === 'dsa' || setup === 'modified';
          const dutyKeep = dsaOrModifieds ? pull.role !== 'dps' : pull.role === 'dps';
          const dutyTrade = dsaOrModifieds ? pull.role === 'dps' : pull.role !== 'dps';
          if (lin === 2) {
            if (pull.hadAccretion || dutyKeep)
              return { infoText: voice.keepTether({ num: num }) };
            return { alertText: voice.passTether({ num: num }) };
          }
          if (lin === 3) {
            const kefkaFacing = pull.kefkaTeleportDirNum;
            const facingNums = pull.blackHoleTetherDirNums;
            const openFacing = kefkaFacing !== undefined
              ? Math.round(kefkaFacing / 2) % 4
              : -1;
            const sorteds = openFacing !== -1 ? readCWSequenceFromN(openFacing, facingNums) : [];
            if (dutyTrade) {
              const sortedFacing = dsaOrModifieds ? sorteds[0] : sorteds[1];
              const afterTwo = sortedFacing !== undefined
                ? Facings.outputFromCardinalNum(sortedFacing)
                : 'unknown';
              const relFacing = relSetup === 'true'
                ? afterTwo
                : dsaOrModifieds
                ? 'clockwiseOne'
                : 'clockwiseTwo';
              return {
                alertText: voice.getDirTether({
                  num: num,
                  dir: voice[relFacing](),
                }),
              };
            }
            const sortedDir2s = dsaOrModifieds ? sorteds[1] : sorteds[0];
            const dir2s = sortedDir2s !== undefined
              ? Facings.outputFromCardinalNum(sortedDir2s)
              : 'unknown';
            const relFacing = relSetup === 'true'
              ? dir2s
              : dsaOrModifieds
              ? 'clockwiseTwo'
              : 'clockwiseOne';
            return {
              infoText: voice.middleThenGetDirTether({
                num: num,
                dir: voice[relFacing](),
              }),
            };
          }
        }

        return { infoText: voice.nothing({ num: trackers }) };
      },
    },
    {
      id: 'DMU P3 Black Hole 4, Nothingness 8',
      type: 'Ability',
      netRegex: { id: 'BAFC', source: 'Black Hole', capture: false },
      condition: (pull) => pull.nothingnessTracker === 8,
      suppressSeconds: 99999,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const trackers = pull.nothingnessTracker;
        const num = voice.num({ num: trackers });
        const lin = pull.inLine[pull.me];

        if (setup !== 'none') {
          const dsaOrModifieds = setup === 'dsa' || setup === 'modified';
          const dutyTrade = dsaOrModifieds ? pull.role !== 'dps' : pull.role === 'dps';
          const dutyKeep = dsaOrModifieds ? pull.role === 'dps' : pull.role !== 'dps';
          if (lin === 2) {
            if (pull.hadAccretion)
              return { infoText: voice.keepTether({ num: num }) };
            if (dutyTrade)
              return { alertText: voice.passTether({ num: num }) };
          }
          if (lin === 3) {
            if (dutyKeep)
              return { infoText: voice.keepTether({ num: num }) };
            const kefkaFacing = pull.kefkaTeleportDirNum;
            const facingNums = pull.blackHoleTetherDirNums;
            const openFacing = kefkaFacing !== undefined
              ? Math.round(kefkaFacing / 2) % 4
              : -1;
            const sorteds = openFacing !== -1 ? readCWSequenceFromN(openFacing, facingNums) : [];
            const sortedFacing = dsaOrModifieds ? sorteds[1] : sorteds[0];
            const afterTwo = sortedFacing !== undefined
              ? Facings.outputFromCardinalNum(sortedFacing)
              : 'unknown';
            const relFacing = relSetup === 'true'
              ? afterTwo
              : dsaOrModifieds
              ? 'clockwiseTwo'
              : 'clockwiseOne';
            return {
              alertText: voice.getDirTether({
                num: num,
                dir: voice[relFacing](),
              }),
            };
          }
        }
        return { infoText: voice.nothing({ num: trackers }) };
      },
    },
    {
      id: 'DMU P3 Black Hole 5, Nothingness 9',
      type: 'SpawnNpcExtra',
      netRegex: { tetherId: headSignState['blackHoleTether'], capture: false },
      condition: (pull) => pull.nothingnessTracker === 9,
      delaySeconds: 0.1,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        if (pull.blackHoleTetherDirNums.length !== 2 || pull.blackHoleTetherDisable)
          return;
        pull.blackHoleTetherDisable = true;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const num = voice.num({ num: pull.nothingnessTracker });
        const kefkaFacing = pull.kefkaTeleportDirNum;
        const facingNums = pull.blackHoleTetherDirNums;

        const openFacing = kefkaFacing !== undefined
          ? Math.round(kefkaFacing / 2) % 4
          : -1;
        const sorteds = openFacing !== -1 ? readCWSequenceFromN(openFacing, facingNums) : [];
        const dir1s = sorteds[0] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[0])
          : 'unknown';
        const dir2s = sorteds[1] !== undefined
          ? Facings.outputFromCardinalNum(sorteds[1])
          : 'unknown';

        if ((setup === 'dsa' || setup === 'sda') && pull.inLine[pull.me] === 3) {
          const facingTwo = pull.role === 'dps' ? dir1s : dir2s;
          const relFacing = relSetup === 'true'
            ? facingTwo
            : pull.role === 'dps'
            ? 'clockwiseOne'
            : 'clockwiseTwo';
          return {
            alertText: voice.getDirTether({
              num: num,
              dir: voice[relFacing](),
            }),
          };
        } else if (
          setup === 'modified' && pull.role !== 'dps' &&
          pull.inLine[pull.me] === 3
        ) {
          return {
            alertText: relSetup === 'true'
              ? voice.getDirTethers({
                num: num,
                dir1: voice[dir1s](),
                dir2: voice[dir2s](),
              })
              : voice.getBothTethers({
                num: num,
              }),
          };
        }

        return {
          infoText: voice.twoBlackHoles({
            num: num,
            dir1: voice[dir1s](),
            dir2: voice[dir2s](),
          }),
        };
      },
    },
    {
      id: 'DMU P3 Black Hole 6, Nothingness 10',
      type: 'Tether',
      netRegex: { id: headSignState['blackHoleTether'], capture: true },
      condition: (pull) => pull.nothingnessTracker === 10,
      delaySeconds: 0.1,
      suppressSeconds: 99999,
      response: (pull, hit, voice) => {
        voice.responseOutputStrings = blackHoleVoiceWords;

        const setup = pull.triggerSetConfig.blackHole;
        const relSetup = pull.triggerSetConfig.blackHoleTether;
        const num = voice.num({ num: pull.nothingnessTracker });
        const facingDigit = pull.blackHoleIdDirNums[hit.sourceId];
        const facingTwo = facingDigit === undefined
          ? 'unknown'
          : Facings.outputFromCardinalNum(facingDigit);

        if (setup !== 'none') {
          const dutyTwo = setup === 'sda' || setup === 'modified'
            ? pull.role === 'dps'
            : pull.role !== 'dps';
          const relFacing = relSetup === 'true' ? facingTwo : 'clockwiseOne';
          if (pull.inLine[pull.me] === 3 && dutyTwo)
            return {
              alertText: voice.getDirTether({
                num: num,
                dir: voice[relFacing](),
              }),
            };
        }
        return {
          infoText: voice.oneBlackHole({
            num: num,
            dir: voice[facingTwo](),
          }),
        };
      },
    },
    {
      id: 'DMU P3 Longitudinal Implosion',
      type: 'StartsUsing',
      netRegex: { id: 'BAFD', source: 'Chaos', capture: false },
      infoText: (_pull, _hit, voice) => voice.sides(),
      outputStrings: { sides: Voices.sidesThenFrontBack },
    },
    {
      id: 'DMU P3 Latitudinal Implosion',
      type: 'StartsUsing',
      netRegex: { id: 'BAFE', source: 'Chaos', capture: false },
      infoText: (_pull, _hit, voice) => voice.frontBack(),
      outputStrings: { frontBack: Voices.frontBackThenSides },
    },
    {
      id: 'DMU P3 Damning Edict',
      type: 'StartsUsing',
      netRegex: { id: 'BB01', source: 'Chaos', capture: true },
      infoText: (_pull, hit, voice) => {
        return voice.getBehindTarget({ target: hit.source });
      },
      outputStrings: {
        getBehindTarget: { en: 'Get Behind ${target}', ko: '${target} \uB4A4\uB85C' },
      },
    },
    {
      id: 'DMU P3 Max Collect ID',
      type: 'StartsUsing',
      netRegex: { id: 'BAE5', source: 'Kefka', capture: true },
      run: (pull, hit) => pull.kefkaId = hit.sourceId,
    },
    {
      id: 'DMU P3 Boss Teleport Collect',
      type: 'ActorControlExtra',
      netRegex: { category: '0197', param1: '1E44', capture: true },
      condition: (pull, hit) => hit.id === pull.kefkaId,
      delaySeconds: 0.1,
      run: (pull) => {
        const bigCode = pull.kefkaId ?? 0;
        const actor = pull.actorPositions[bigCode];
        if (actor === undefined)
          return;
        pull.kefkaTeleportDirNum = (Facings.hdgTo8DirNum(actor.heading) + 4) % 8;
      },
    },
    {
      id: 'DMU P3 Boss Teleport Location',
      type: 'ActorControlExtra',
      netRegex: { category: '0197', param1: '1E44', capture: true },
      condition: (pull, hit) => hit.id === pull.kefkaId && pull.nothingnessTracker !== 9,
      delaySeconds: 0.1,
      infoText: (pull, _hit, voice) => {
        const bigCode = pull.kefkaId ?? 0;
        const actor = pull.actorPositions[bigCode];
        if (actor === undefined)
          return;
        const facingDigit = (Facings.hdgTo8DirNum(actor.heading) + 4) % 8;
        const facingTwo = Facings.outputFrom8DirNum(facingDigit);
        return voice.text({ dir: voice[facingTwo]() });
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        text: { en: '${dir} Kefka' },
      },
    },
    {
      id: 'DMU P3 White Hole + Boss Teleport Location',
      type: 'ActorControlExtra',
      netRegex: { category: '0197', param1: '1E44', capture: true },
      condition: (pull, hit) => hit.id === pull.kefkaId && pull.nothingnessTracker === 9,
      delaySeconds: 0.1,
      durationSeconds: 9.1,
      suppressSeconds: 99999,
      alertText: (pull, _hit, voice) => {
        const bigCode = pull.kefkaId ?? 0;
        const actor = pull.actorPositions[bigCode];
        if (actor === undefined)
          return;
        const facingDigit = (Facings.hdgTo8DirNum(actor.heading) + 4) % 8;
        const facingTwo = Facings.outputFrom8DirNum(facingDigit);
        return voice.text({
          heal: voice.fullHeal(),
          dir: voice.dirKefka({ dir: voice[facingTwo]() }),
        });
      },
      outputStrings: {
        ...Facings.outputStrings8Dir,
        fullHeal: { en: 'Heal to full' },
        dirKefka: { en: '${dir} Kefka' },
        text: { en: '${heal} + ${dir}' },
      },
    },
    {
      id: 'DMU P3 Vacuum Wave Tank LB3',
      type: 'StartsUsing',
      netRegex: { id: 'BB13', source: 'Exdeath', capture: true },
      condition: (pull) => pull.role === 'tank' && pull.triggerSetConfig.boa === 'lb3',
      delaySeconds: (_pull, hit) => parseFloat(hit.castTime) - 2,
      alarmText: (_pull, _hit, voice) => voice.text(),
      outputStrings: { text: { en: 'TANK LB!!' } },
    },
    {
      id: 'DMU P3 Look upon Me and Despair',
      type: 'StartsUsing',
      netRegex: { id: 'BAEC', source: 'Kefka', capture: false },
      alertText: (_pull, _hit, voice) => voice.outOfMiddle(),
      outputStrings: { outOfMiddle: { en: 'Out Of Middle' } },
    },
    {
      id: 'DMU P3 Look upon Me and Despair 2',
      type: 'StartsUsing',
      netRegex: { id: 'BAED', source: 'Kefka', capture: false },
      alertText: (_pull, _hit, voice) => voice.outOfMiddle(),
      outputStrings: { outOfMiddle: { en: 'Out Of Middle' } },
    },
    {
      id: 'DMU P3 Knock Down 1',
      type: 'HeadMarker',
      netRegex: { id: headSignState['stompStack'], capture: true },
      suppressSeconds: 99999,
      alertText: (pull, hit, voice) => {
        const markThree = hit.target;
        if (markThree === pull.me)
          return voice.mechThenMech({ mech1: voice.stackOnYou(), mech2: voice.getTowers() });
        const isDPSPile = pull.party.isDPS(hit.target);
        const amDPSBit = pull.role === 'dps';
        if ((isDPSPile && amDPSBit) || (!isDPSPile && !amDPSBit))
          return voice.mechThenMech({
            mech1: voice.stackOnPlayer({ player: pull.party.member(markThree) }),
            mech2: voice.getTowers(),
          });
        return voice.mechThenMech({ mech1: voice.getTowers(), mech2: voice.stack() });
      },
      outputStrings: {
        getTowers: Voices.getTowers,
        stackOnYou: Voices.stackOnYou,
        stackOnPlayer: Voices.stackOnPlayer,
        stack: Voices.stackMarker,
        mechThenMech: { en: '${mech1} => ${mech2}' },
      },
    },
    {
      id: 'DMU P3 Knock Down State',
      type: 'Ability',
      netRegex: { id: 'BB02', source: 'Chaos', capture: false },
      suppressSeconds: 99999,
      run: (pull) => pull.isKnockDown2 = true,
    },
    {
      id: 'DMU P3 Knock Down 2',
      type: 'HeadMarker',
      netRegex: { id: headSignState['stompStack'], capture: true },
      condition: (pull) => pull.isKnockDown2,
      alertText: (pull, hit, voice) => {
        const markThree = hit.target;
        if (markThree === pull.me)
          return voice.stackOnYou();
        const isDPSPile = pull.party.isDPS(hit.target);
        const amDPSBit = pull.role === 'dps';
        if ((isDPSPile && amDPSBit) || (!isDPSPile && !amDPSBit))
          return voice.stackOnPlayer({ player: pull.party.member(markThree) });
        return voice.getTowers();
      },
      outputStrings: {
        getTowers: Voices.getTowers,
        stackOnYou: Voices.stackOnYou,
        stackOnPlayer: Voices.stackOnPlayer,
      },
    },
    {
      id: 'DMU P3 Blizzard III Puddles',
      type: 'StartsUsing',
      netRegex: { id: 'BB0F', source: 'Exdeath', capture: false },
      infoText: (_pull, _hit, voice) => voice.baitPuddles(),
      outputStrings: { baitPuddles: { en: 'Bait Puddles x2' } },
    },
    {
      id: 'DMU P3 Blizzard III Keep Moving',
      type: 'StartsUsing',
      netRegex: { id: 'BB11', source: 'Exdeath', capture: true },
      durationSeconds: (_pull, hit) => parseFloat(hit.castTime),
      infoText: (_pull, _hit, voice) => voice.keepMoving(),
      outputStrings: { keepMoving: Voices.moveAround },
    },
    {
      id: 'DMU P4 Real/Fake VFX Tracker',
      type: 'GainsEffect',
      netRegex: { effectId: '808', capture: true },
      run: (pull, hit) => {
        const v = parseInt(hit.count, 16);
        if (hit.target === 'Neo Exdeath')
          pull.neReal = v === 1122;
        else if (hit.target === 'Chaos')
          pull.chReal = v === 1120;
      },
    },
    {
      id: 'DMU P4 Chaos and Neo Exdeath Debuff Collect',
      type: 'GainsEffect',
      netRegex: { effectId: '808', count: ['45F', '460', '461', '462'], capture: true },
      run: (pull, hit) => {
        const tally = hit.count;
        const isTru = tally === '460' || tally === '462';
        if (pull.areFirstDebuffsTrue === undefined)
          pull.areFirstDebuffsTrue = isTru;
        else if (pull.areSecondDebuffsTrue === undefined)
          pull.areSecondDebuffsTrue = isTru;
        else if (pull.areThirdDebuffsTrue === undefined)
          pull.areThirdDebuffsTrue = isTru;
        else if (pull.areFourthDebuffsTrue === undefined)
          pull.areFourthDebuffsTrue = isTru;
      },
    },
    {
      id: 'DMU P4 Second and Fourth Debuffs (Early)',
      type: 'StartsUsing',
      netRegex: { id: ['BB20', 'BB21'], capture: true },
      delaySeconds: 1,
      infoText: (pull, hit, voice) => {
        const isTru = pull.areFourthDebuffsTrue !== undefined
          ? pull.areFourthDebuffsTrue
          : pull.areSecondDebuffsTrue;
        if (isTru === undefined)
          return;

        const isInfern = hit.id === 'BB20';
        if (isInfern)
          return isTru ? voice.puddlesFirst() : voice.donutsFirst();
        return isTru ? voice.donutsSecond() : voice.puddlesSecond();
      },
      outputStrings: {
        puddlesFirst: { en: 'Puddles First' },
        puddlesSecond: { en: 'Puddles Second' },
        donutsFirst: { en: 'Donuts First' },
        donutsSecond: { en: 'Donuts Second' },
      },
    },
    {
      id: 'DMU P4 Grand Cross Counter',
      type: 'StartsUsing',
      netRegex: { id: 'BB14', source: 'Neo Exdeath', capture: false },
      run: (pull) => pull.grandCrossCount = pull.grandCrossCount + 1,
    },
    {
      id: 'DMU P4 Grand Cross',
      type: 'StartsUsing',
      netRegex: { id: 'BB14', source: 'Neo Exdeath', capture: true },
      delaySeconds: (_pull, hit) => parseFloat(hit.castTime) - 5,
      response: Response.aoe(),
    },
    {
      id: 'DMU P4 Debuff Collect',
      type: 'GainsEffect',
      netRegex: {
        effectId: [
          '15A5',
          '15A6',
          '15A7',
          '15A8',
          '15A9',
          '15AA',
          '1317',
          '1318',
          '1558',
          '566',
          '1C6',
        ],
        capture: true,
      },
      run: (pull, hit) => {
        const markThree = hit.target;
        const id = hit.effectId;
        const durations = parseFloat(hit.duration);

        if (id === '15A7') {
          if (durations < 61)
            pull.shortShriekPlayers.push(markThree);
          else
            pull.longShriekPlayers.push(markThree);
        } else if (id === '15A8') {
          if (durations < 52) {
            if (pull.isFirstDebuffShort === undefined)
              pull.isFirstDebuffShort = true;
            pull.shortForkedPlayers.push(markThree);
          } else {
            if (pull.isFirstDebuffShort === undefined)
              pull.isFirstDebuffShort = false;
            pull.longForkedPlayers.push(markThree);
          }
        } else if (id === '15A9') {
          if (durations < 52) {
            pull.shortCompressedPlayers.push(markThree);
          } else
            pull.longCompressedPlayers.push(markThree);
        } else if (id === '15AA') {
          if (durations < 37)
            pull.secondShortBombPlayers.push(markThree);
          else if (durations < 52)
            pull.firstShortBombPlayers.push(markThree);
          else if (durations < 62)
            pull.secondLongBombPlayers.push(markThree);
          else
            pull.firstLongBombPlayers.push(markThree);
        } else if (pull.me === markThree) {
          if (id === '1558' || id === '566')
            pull.deathOrField = 'death';
          else if (id === '1C6')
            pull.deathOrField = 'field';
          else if (id === '1317' || id === '15A5')
            pull.wound = 'white';
          else if (id === '1318' || id === '15A6')
            pull.wound = 'black';
        }
      },
    },
    {
      id: 'DMU P4 Tsunami/Inferno and First Debuffs (Early)',
      type: 'GainsEffect',
      netRegex: { effectId: ['15A7', '15A8', '15A9', '15AA'], capture: true },
      condition: Condition.targetIsYou(),
      delaySeconds: 0.1,
      suppressSeconds: 99999,
      infoText: (pull, _hit, voice) => {
        const isTru = pull.areFirstDebuffsTrue;
        const isShorts = pull.isFirstDebuffShort;
        if (isTru === undefined || isShorts === undefined)
          return voice.aoe();

        const hasShreiks = pull.shortShriekPlayers.includes(pull.me);
        const hasForks = isShorts
          ? pull.shortForkedPlayers.includes(pull.me)
          : pull.longForkedPlayers.includes(pull.me);
        const hasCompresseds = isShorts
          ? pull.shortCompressedPlayers.includes(pull.me)
          : pull.longCompressedPlayers.includes(pull.me);
        const hasLeadBomb = pull.firstShortBombPlayers.includes(pull.me);
        const hasSecondBombs = pull.firstLongBombPlayers.includes(pull.me);

        if (hasShreiks)
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: voice.firstGazeAndBomb({
              gaze: isTru ? voice.gaze() : voice.fakeGaze(),
              bomb: isTru ? voice.bomb() : voice.fakeBomb(),
            }),
          });

        if ((hasForks && isTru) || (hasCompresseds && !isTru))
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: isShorts
              ? voice.spreadFirst({ mech: voice.spread() })
              : voice.spreadSecond({ mech: voice.spread() }),
          });
        if ((hasForks && !isTru) || (hasCompresseds && isTru))
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: isShorts
              ? voice.stackFirst({ mech: voice.stack() })
              : voice.stackSecond({ mech: voice.stack() }),
          });
        if (hasLeadBomb)
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: voice.bombFirst({
              mech: isTru ? voice.bomb() : voice.fakeBomb(),
            }),
          });
        if (hasSecondBombs)
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: voice.bombSecond({
              mech: isTru ? voice.bomb() : voice.fakeBomb(),
            }),
          });

        return voice.aoeDebuff({
          aoe: voice.aoe(),
          debuff: isShorts
            ? voice.stackFirstNoDebuff({ mech: voice.stack() })
            : voice.stackSecondNoDebuff({ mech: voice.stack() }),
        });
      },
      outputStrings: {
        aoe: Voices.aoe,
        aoeDebuff: { en: '${aoe} + ${debuff}' },
        firstGazeAndBomb: { en: '${gaze} + ${bomb} on YOU First' },
        gaze: { en: 'Look Away' },
        fakeGaze: { en: 'Look At' },
        spreadFirst: { en: '${mech} on YOU First' },
        stackFirst: { en: '${mech} on YOU First' },
        stackFirstNoDebuff: { en: 'No Debuff, ${mech} First' },
        bombFirst: { en: '${mech} on YOU First' },
        stackSecondNoDebuff: { en: 'No Debuff, ${mech} Second' },
        stackSecond: { en: '${mech} on YOU Second' },
        spreadSecond: { en: '${mech} on YOU Second' },
        bombSecond: { en: '${mech} on YOU Second' },
        stack: Voices.stackMarker,
        spread: Voices.spread,
        bomb: { en: 'Stillness' },
        fakeBomb: { en: 'Motion' },
      },
    },
    {
      id: 'DMU P4 Entropy/Dynamic Fluid Collect',
      type: 'GainsEffect',
      netRegex: { effectId: ['15AB', '15AC'], capture: true },
      suppressSeconds: 1,
      run: (pull, hit) => {
        const durations = parseFloat(hit.duration);

        if (hit.effectId === '15AB') {
          pull.isEntropyTrue = durations > 46
            ? pull.areSecondDebuffsTrue
            : pull.areFourthDebuffsTrue;
          return;
        }
        pull.isFluidTrue = durations > 83
          ? pull.areSecondDebuffsTrue
          : pull.areFourthDebuffsTrue;
      },
    },
    {
      id: 'DMU P4 Tsunami/Inferno and Third Debuffs (Early)',
      type: 'GainsEffect',
      netRegex: { effectId: ['15A7', '15A8', '15A9', '15AA'], capture: true },
      condition: (pull, hit) => {
        return pull.me === hit.target && pull.grandCrossCount === 2;
      },
      delaySeconds: 0.1,
      suppressSeconds: 99999,
      infoText: (pull, _hit, voice) => {
        const isTru = pull.areThirdDebuffsTrue;
        const isShorts = pull.isFirstDebuffShort;
        if (isTru === undefined || isShorts === undefined)
          return voice.aoe();

        const hasShreiks = pull.longShriekPlayers.includes(pull.me);
        const hasForks = isShorts
          ? pull.longForkedPlayers.includes(pull.me)
          : pull.shortForkedPlayers.includes(pull.me);
        const hasCompresseds = isShorts
          ? pull.longCompressedPlayers.includes(pull.me)
          : pull.shortCompressedPlayers.includes(pull.me);
        const hasLeadBomb = pull.secondShortBombPlayers.includes(pull.me);
        const hasSecondBombs = pull.secondLongBombPlayers.includes(pull.me);

        if (hasShreiks)
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: voice.secondGazeAndBomb({
              gaze: isTru ? voice.gaze() : voice.fakeGaze(),
              bomb: isTru ? voice.bomb() : voice.fakeBomb(),
            }),
          });

        if ((hasForks && isTru) || (hasCompresseds && !isTru))
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: isShorts
              ? voice.spreadSecond({ mech: voice.spread() })
              : voice.spreadFirst({ mech: voice.spread() }),
          });
        if ((hasForks && !isTru) || (hasCompresseds && isTru))
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: isShorts
              ? voice.stackSecond({ mech: voice.stack() })
              : voice.stackFirst({ mech: voice.stack() }),
          });
        if (hasLeadBomb)
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: voice.bombFirst({
              mech: isTru ? voice.bomb() : voice.fakeBomb(),
            }),
          });
        if (hasSecondBombs)
          return voice.aoeDebuff({
            aoe: voice.aoe(),
            debuff: voice.bombSecond({
              mech: isTru ? voice.bomb() : voice.fakeBomb(),
            }),
          });

        return voice.aoeDebuff({
          aoe: voice.aoe(),
          debuff: isShorts
            ? voice.stackSecondNoDebuff({ mech: voice.stack() })
            : voice.stackFirstNoDebuff({ mech: voice.stack() }),
        });
      },
      outputStrings: {
        aoe: Voices.aoe,
        aoeDebuff: { en: '${aoe} + ${debuff}' },
        secondGazeAndBomb: { en: '${gaze} + ${bomb} on YOU Second' },
        gaze: { en: 'Look Away' },
        fakeGaze: { en: 'Look At' },
        spreadFirst: { en: '${mech} on YOU First' },
        stackFirst: { en: '${mech} on YOU First' },
        bombFirst: { en: '${mech} on YOU First' },
        stackFirstNoDebuff: { en: 'No Debuff, ${mech} First' },
        stackSecondNoDebuff: { en: 'No Debuff, ${mech} Second' },
        spreadSecond: { en: '${mech} on YOU Second' },
        stackSecond: { en: '${mech} on YOU Second' },
        bombSecond: { en: '${mech} on YOU Second' },
        stack: Voices.stackMarker,
        spread: Voices.spread,
        bomb: { en: 'Stillness' },
        fakeBomb: { en: 'Motion' },
      },
    },
    {
      id: 'DMU P4 Fifth Debuffs',
      type: 'GainsEffect',
      netRegex: {
        effectId: ['1317', '15A5', '1318', '15A6', '566', '1558', '1C6'],
        capture: true,
      },
      condition: Condition.targetIsYou(),
      delaySeconds: 0.1,
      suppressSeconds: 99999,
      infoText: (pull, _hit, voice) => {
        const wounds = pull.wound;
        const deathOrFields = pull.deathOrField;
        if (wounds === undefined || deathOrFields === undefined)
          return;

        return voice.debuffsOnYou({
          wound: voice[wounds](),
          deathOrField: voice[deathOrFields](),
        });
      },
      outputStrings: {
        death: { en: 'Death' },
        field: { en: 'Field' },
        white: { en: 'Purple Debuff' },
        black: { en: 'Blue Debuff' },
        debuffsOnYou: { en: '${wound} + ${deathOrField} on YOU' },
      },
    },
    {
      id: 'DMU P4 Flood of Naught',
      type: 'StartsUsing',
      netRegex: { id: ['C392', 'C393', 'C3A1', 'C3A2'], source: 'Neo Exdeath', capture: true },
      alertText: (pull, hit, voice) => {
        const wounds = pull.wound;
        const deathOrFields = pull.deathOrField;
        if (wounds === undefined || deathOrFields === undefined)
          return;

        const id = hit.id;
        const isFloodTru = id === 'C392' || id === 'C393';
        const isBluePort = id === 'C3A2' || id === 'C393';
        const keeps = (deathOrFields === 'death' && isFloodTru) ||
          (deathOrFields === 'field' && !isFloodTru);

        const lasers = voice[deathOrFields]({
          color: keeps
            ? voice[wounds]()
            : wounds === 'white'
            ? voice.black()
            : voice.white(),
          dir: (keeps && wounds === 'white' && isBluePort) ||
              (keeps && wounds === 'black' && !isBluePort) ||
              (!keeps && wounds === 'white' && !isBluePort) ||
              (!keeps && wounds === 'black' && isBluePort)
            ? voice.right()
            : voice.left(),
        });

        const is1stTru = pull.areFirstDebuffsTrue;
        const is2ndTru = pull.areThirdDebuffsTrue;
        const isLeadShort = pull.isFirstDebuffShort;
        if (is1stTru === undefined || is2ndTru === undefined || isLeadShort === undefined)
          return lasers;
        const isShortTru = isLeadShort ? is1stTru : is2ndTru;

        const hasForks = pull.shortForkedPlayers.includes(pull.me);
        const hasCompresseds = pull.shortCompressedPlayers.includes(pull.me);
        const hasLeadBomb = pull.firstShortBombPlayers.includes(pull.me);
        const hasSecondBombs = pull.secondShortBombPlayers.includes(pull.me);
        const isBombTru = hasLeadBomb ? is1stTru : is2ndTru;

        const hasScatter = (hasForks && isShortTru) || (hasCompresseds && !isShortTru);
        const hasPile = (hasForks && !isShortTru) || (hasCompresseds && isShortTru);
        const hasBombs = hasLeadBomb || hasSecondBombs;

        if (hasScatter && hasBombs)
          return voice.laserThenForkBomb({
            mech1: lasers,
            mech2: voice.spread(),
            mech3: isBombTru ? voice.bomb() : voice.fakeBomb(),
          });
        if (hasPile && hasBombs)
          return voice.laserThenCompressedBomb({
            mech1: lasers,
            mech2: voice.stack(),
            mech3: isBombTru ? voice.bomb() : voice.fakeBomb(),
          });
        if (hasScatter)
          return voice.laserThenSpread({
            mech1: lasers,
            mech2: voice.spread(),
          });
        if (hasPile)
          return voice.laserThenStack({
            mech1: lasers,
            mech2: voice.stack(),
          });
        if (hasBombs)
          return voice.laserThenBomb({
            mech1: lasers,
            mech2: isBombTru ? voice.bomb() : voice.fakeBomb(),
            mech3: voice.stack(),
          });
        return voice.laserThenNoDebuff({
          mech1: lasers,
          mech2: voice.noDebuff(),
        });
      },
      outputStrings: {
        death: { en: 'Stand in ${color} (${dir})' },
        field: { en: 'Stand in ${color} (${dir})' },
        white: { en: 'Purple' },
        black: { en: 'Blue' },
        left: Voices.left,
        right: Voices.right,
        laserThenSpread: { en: '${mech1} => ${mech2}' },
        laserThenStack: { en: '${mech1} => ${mech2}' },
        laserThenBomb: { en: '${mech1} => ${mech2} + ${mech3}' },
        laserThenForkBomb: { en: '${mech1} => ${mech2} + ${mech3}' },
        laserThenCompressedBomb: { en: '${mech1} => ${mech2} + ${mech3}' },
        laserThenNoDebuff: { en: '${mech1} => ${mech2}' },
        noDebuff: Voices.stackMarker,
        stack: Voices.stackMarker,
        spread: Voices.spread,
        bomb: { en: 'Stillness' },
        fakeBomb: { en: 'Motion' },
      },
    },
    {
      id: 'DMU P4 Short Debuffs',
      type: 'Ability',
      netRegex: { id: ['C394', 'C395'], source: 'Neo Exdeath', capture: false },
      suppressSeconds: 99999,
      alertText: (pull, _hit, voice) => {
        const is1stTru = pull.areFirstDebuffsTrue;
        const is2ndTru = pull.areThirdDebuffsTrue;
        const isLeadShort = pull.isFirstDebuffShort;
        if (is1stTru === undefined || is2ndTru === undefined || isLeadShort === undefined)
          return;
        const isShortTru = isLeadShort ? is1stTru : is2ndTru;

        const hasForks = pull.shortForkedPlayers.includes(pull.me);
        const hasCompresseds = pull.shortCompressedPlayers.includes(pull.me);
        const hasLeadBomb = pull.firstShortBombPlayers.includes(pull.me);
        const hasSecondBombs = pull.secondShortBombPlayers.includes(pull.me);
        const isBombTru = hasLeadBomb ? is1stTru : is2ndTru;

        const hasScatter = (hasForks && isShortTru) || (hasCompresseds && !isShortTru);
        const hasPile = (hasForks && !isShortTru) || (hasCompresseds && isShortTru);
        const hasBombs = hasLeadBomb || hasSecondBombs;

        if (hasScatter && hasBombs)
          return voice.forkBomb({
            mech1: voice.spread(),
            mech2: isBombTru ? voice.bomb() : voice.fakeBomb(),
          });
        if (hasPile && hasBombs)
          return voice.compressedBomb({
            mech1: voice.stack(),
            mech2: isBombTru ? voice.bomb() : voice.fakeBomb(),
          });
        if (hasScatter)
          return voice.spread();
        if (hasPile)
          return voice.stack();
        if (hasBombs)
          return voice.bombStack({
            mech1: isBombTru ? voice.bomb() : voice.fakeBomb(),
            mech2: voice.stack(),
          });
        return voice.noDebuff();
      },
      outputStrings: {
        you: { en: 'YOU' },
        bombStack: { en: '${mech1} + ${mech2}' },
        forkBomb: { en: '${mech1} + ${mech2}' },
        compressedBomb: { en: '${mech1} + ${mech2}' },
        noDebuff: Voices.stackMarker,
        stack: Voices.stackMarker,
        spread: Voices.spread,
        bomb: { en: 'Stillness' },
        fakeBomb: { en: 'Motion' },
      },
    },
    {
      id: 'DMU P4 Acceleration Bomb Reminder',
      type: 'GainsEffect',
      netRegex: { effectId: '15AA', capture: true },
      condition: Condition.targetIsYou(),
      delaySeconds: (_pull, hit) => parseFloat(hit.duration) - 3,
      durationSeconds: 3,
      alertText: (pull, hit, voice) => {
        const durations = parseFloat(hit.duration);
        const isLeadAura = durations > 75 || (durations < 52 && durations > 50);
        const isTru = isLeadAura
          ? pull.areFirstDebuffsTrue
          : pull.areThirdDebuffsTrue;
        return isTru ? voice.stopEverything() : voice.keepMoving();
      },
      outputStrings: {
        keepMoving: { en: 'Keep Moving' },
        stopEverything: { en: 'Stop Everything' },
      },
    },
    {
      id: 'DMU P4 Cursed Shriek (Early)',
      type: 'GainsEffect',
      netRegex: { effectId: '15A7', capture: true },
      delaySeconds: (_pull, hit) => {
        return parseFloat(hit.duration) < 61 ? 51.1 : 61.1;
      },
      suppressSeconds: 1,
      infoText: (pull, hit, voice) => {
        const isShortDebuff = parseFloat(hit.duration) < 61;
        const isTru = isShortDebuff ? pull.areFirstDebuffsTrue : pull.areThirdDebuffsTrue;
        if (isTru === undefined)
          return;

        const shriekMembers = isShortDebuff ? pull.shortShriekPlayers : pull.longShriekPlayers;
        const members = shriekMembers.map(
          (player) => {
            if (player === pull.me)
              return voice.you();
            return pull.party.member(player);
          },
        );
        const msgs = members.join(', ');

        if (shriekMembers.includes(pull.me)) {
          return isTru
            ? voice.gazeOnYou({ players: msgs })
            : voice.fakeGazeOnYou({ players: msgs });
        }
        return isTru
          ? voice.gazeOnPlayers({ players: msgs })
          : voice.fakeGazeOnPlayers({ players: msgs });
      },
      outputStrings: {
        you: { en: 'YOU' },
        fakeGazeOnPlayers: { en: 'Face ${players} (later)' },
        gazeOnPlayers: { en: 'Look Away from ${players} (later)' },
        fakeGazeOnYou: { en: 'Face ${players} (later)' },
        gazeOnYou: { en: 'Look Away from ${players} (later)' },
      },
    },
    {
      id: 'DMU P4 Mana Charge Collect',
      type: 'GainsEffect',
      netRegex: { effectId: ['5CD', '5CC'], capture: true },
      delaySeconds: 0.1,
      run: (pull, hit) => {
        if (hit.effectId === '5CD')
          pull.isThunderChargedTrue = pull.isThunderTrue;
        else
          pull.isBlizzardChargedTrue = pull.isIceTrue;
      },
    },
    {
      id: 'DMU P4 Thrumming Thunder III',
      type: 'StartsUsing',
      netRegex: { id: 'C5DE', source: 'Kefka', capture: false },
      condition: (pull) => pull.isThunderTrue !== undefined,
      infoText: (pull, _hit, voice) => {
        return pull.isThunderTrue ? voice.trueThunder() : voice.fakeThunder();
      },
      outputStrings: mysteryMagicVoiceWords,
    },
    {
      id: 'DMU P4 First Cursed Shriek',
      type: 'GainsEffect',
      netRegex: { effectId: '15A7', capture: true },
      condition: (_pull, hit) => parseFloat(hit.duration) < 61,
      delaySeconds: (_pull, hit) => parseFloat(hit.duration) - 3,
      durationSeconds: 3,
      suppressSeconds: 99999,
      alertText: (pull, _hit, voice) => {
        const is1stTru = pull.areFirstDebuffsTrue;
        if (is1stTru === undefined)
          return;

        const shriekMembers = pull.shortShriekPlayers;
        const members = shriekMembers.map(
          (player) => {
            if (player === pull.me)
              return voice.you();
            return pull.party.member(player);
          },
        );
        const msgs = members.join(', ');

        if (shriekMembers.includes(pull.me))
          return is1stTru
            ? voice.gazeOnPlayersYou({ players: msgs })
            : voice.fakeGazeOnPlayersYou({ players: msgs });
        return is1stTru
          ? voice.gazeOnPlayers({ players: msgs })
          : voice.fakeGazeOnPlayers({ players: msgs });
      },
      outputStrings: {
        you: { en: 'YOU' },
        fakeGazeOnPlayers: { en: 'Face ${players}' },
        gazeOnPlayers: { en: 'Look Away from ${players}' },
        fakeGazeOnPlayersYou: { en: 'Face ${players}' },
        gazeOnPlayersYou: { en: 'Look Away from ${players}' },
      },
    },
    {
      id: 'DMU P4 Entropy',
      type: 'GainsEffect',
      netRegex: { effectId: '15A7', capture: true },
      condition: (_pull, hit) => parseFloat(hit.duration) < 61,
      delaySeconds: (_pull, hit) => parseFloat(hit.duration),
      durationSeconds: 6.9,
      suppressSeconds: 99999,
      alertText: (pull, _hit, voice) => {
        const isEntropyTru = pull.isEntropyTrue;
        if (isEntropyTru === undefined)
          return;

        return isEntropyTru ? voice.puddles() : voice.donuts();
      },
      outputStrings: {
        donuts: { en: 'Stack for Donuts' },
        puddles: Voices.baitPuddles,
      },
    },
    {
      id: 'DMU P4 Shotcall Gaze',
      type: 'GainsEffect',
      netRegex: { effectId: '15A7', capture: true },
      condition: (pull, hit) => {
        if (pull.phase !== 'p4' || pull.neReal === undefined)
          return false;
        const atBit = parseFloat(hit.duration) < 65 ? pull.p4ShotGaze1At : pull.p4ShotGaze2At;
        return atBit === undefined || Date.now() - atBit > 10000;
      },
      preRun: (pull, hit) => {
        if (parseFloat(hit.duration) < 65) {
          pull.p4ShotGaze1At = Date.now();
          pull.p4ShotGaze1Real = pull.neReal;
        } else {
          pull.p4ShotGaze2At = Date.now();
          pull.p4ShotGaze2Real = pull.neReal;
        }
      },
      delaySeconds: 1,
      macroText: (pull, hit, voice) => {
        const shorts = parseFloat(hit.duration) < 65;
        const reals = shorts ? pull.p4ShotGaze1Real : pull.p4ShotGaze2Real;
        if (reals)
          return shorts ? voice.realGaze1() : voice.realGaze2();
        return shorts ? voice.fakeGaze1() : voice.fakeGaze2();
      },
      outputStrings: {
        realGaze1: { en: 'Gaze1: Look OUT.' },
        fakeGaze1: { en: 'Gaze1: Look INSIDE.' },
        realGaze2: { en: 'Gaze2: Look OUT.' },
        fakeGaze2: { en: 'Gaze2: Look INSIDE.' },
      },
    },
    {
      id: 'DMU P4 Shotcall Chaos',
      type: 'GainsEffect',
      netRegex: { effectId: ['15AB', '15AC'], capture: true },
      condition: (pull, hit) => {
        if (pull.phase !== 'p4' || pull.chReal === undefined)
          return false;
        const atBit = hit.effectId === '15AB' ? pull.p4ShotFireAt : pull.p4ShotWaterAt;
        return atBit === undefined || Date.now() - atBit > 10000;
      },
      preRun: (pull, hit) => {
        if (hit.effectId === '15AB') {
          pull.p4ShotFireAt = Date.now();
          pull.p4ShotFireReal = pull.chReal;
        } else {
          pull.p4ShotWaterAt = Date.now();
          pull.p4ShotWaterReal = pull.chReal;
        }
      },
      delaySeconds: 1,
      macroText: (pull, hit, voice) => {
        if (hit.effectId === '15AB')
          return pull.p4ShotFireReal ? voice.realInferno() : voice.fakeInferno();
        return pull.p4ShotWaterReal ? voice.realTsunami() : voice.fakeTsunami();
      },
      outputStrings: {
        realInferno: { en: 'Fire is AOE (dodge)' },
        fakeInferno: { en: 'Fire is DYNAMO (stay)' },
        realTsunami: { en: 'Water is DYNAMO (stay)' },
        fakeTsunami: { en: 'Water is AOE (dodge)' },
      },
    },
    {
      id: 'DMU P4 Stray Flames and Long Debuffs',
      type: 'GainsEffect',
      netRegex: { effectId: '15AB', capture: true },
      delaySeconds: (pull, hit) => {
        const durations = parseFloat(hit.duration);
        return (pull.isEntropyTrue || pull.isEntropyTrue === undefined)
          ? durations
          : durations + 4.7;
      },
      durationSeconds: (pull) => {
        return (pull.isEntropyTrue || pull.isEntropyTrue === undefined) ? 9.1 : 4.4;
      },
      suppressSeconds: 99999,
      response: (pull, _hit, voice) => {
        voice.responseOutputStrings = {
          you: { en: 'YOU' },
          bombStack: { en: '${mech1} + ${mech2}' },
          forkBomb: { en: '${mech1} + ${mech2}' },
          compressedBomb: { en: '${mech1} + ${mech2}' },
          noDebuff: Voices.stackMarker,
          stack: Voices.stackMarker,
          spread: Voices.spread,
          bomb: { en: 'Stillness' },
          fakeBomb: { en: 'Motion' },
        };

        const is1stTru = pull.areFirstDebuffsTrue;
        const is2ndTru = pull.areThirdDebuffsTrue;
        const isLeadShort = pull.isFirstDebuffShort;
        if (is1stTru === undefined || is2ndTru === undefined || isLeadShort === undefined)
          return;
        const isLongTru = isLeadShort ? is2ndTru : is1stTru;

        const severities = pull.isEntropyTrue === undefined ? 'infoText' : 'alertText';

        const hasForks = pull.longForkedPlayers.includes(pull.me);
        const hasCompresseds = pull.longCompressedPlayers.includes(pull.me);
        const hasLeadBomb = pull.firstLongBombPlayers.includes(pull.me);
        const hasSecondBombs = pull.secondLongBombPlayers.includes(pull.me);
        const isBombTru = hasLeadBomb ? is1stTru : is2ndTru;

        const hasScatter = (hasForks && isLongTru) || (hasCompresseds && !isLongTru);
        const hasPile = (hasForks && !isLongTru) || (hasCompresseds && isLongTru);
        const hasBombs = hasLeadBomb || hasSecondBombs;

        if (hasScatter && hasBombs)
          return {
            [severities]: voice.forkBomb({
              mech1: voice.spread(),
              mech2: isBombTru ? voice.bomb() : voice.fakeBomb(),
            }),
          };
        if (hasPile && hasBombs)
          return {
            [severities]: voice.compressedBomb({
              mech1: voice.stack(),
              mech2: isBombTru ? voice.bomb() : voice.fakeBomb(),
            }),
          };
        if (hasScatter)
          return { [severities]: voice.spread() };
        if (hasPile)
          return { [severities]: voice.stack() };
        if (hasBombs)
          return {
            [severities]: voice.bombStack({
              mech1: isBombTru ? voice.bomb() : voice.fakeBomb(),
              mech2: voice.stack(),
            }),
          };
        return { [severities]: voice.noDebuff() };
      },
    },
    {
      id: 'DMU P4 Blizzard III Blowout',
      type: 'StartsUsing',
      netRegex: { id: 'BA95', source: 'Kefka', capture: false },
      condition: (pull) => (pull.grandCrossCount === 3 && pull.isIceTrue !== undefined),
      infoText: (pull, _hit, voice) => {
        return pull.isIceTrue ? voice.trueIce() : voice.fakeIce();
      },
      outputStrings: mysteryMagicVoiceWords,
    },
    {
      id: 'DMU P4 Second Cursed Shriek',
      type: 'GainsEffect',
      netRegex: { effectId: '15A7', capture: true },
      condition: (_pull, hit) => parseFloat(hit.duration) > 68,
      delaySeconds: (_pull, hit) => parseFloat(hit.duration) - 3,
      durationSeconds: 3,
      suppressSeconds: 99999,
      alertText: (pull, _hit, voice) => {
        const is2ndTru = pull.areThirdDebuffsTrue;
        if (is2ndTru === undefined)
          return;

        const shriekMembers = pull.longShriekPlayers;
        const members = shriekMembers.map(
          (player) => {
            if (player === pull.me)
              return voice.you();
            return pull.party.member(player);
          },
        );
        const msgs = members.join(', ');

        if (shriekMembers.includes(pull.me))
          return is2ndTru
            ? voice.gazeOnPlayersYou({ players: msgs })
            : voice.fakeGazeOnPlayersYou({ players: msgs });
        return is2ndTru
          ? voice.gazeOnPlayers({ players: msgs })
          : voice.fakeGazeOnPlayers({ players: msgs });
      },
      outputStrings: {
        you: { en: 'YOU' },
        fakeGazeOnPlayers: { en: 'Face ${players}' },
        gazeOnPlayers: { en: 'Look Away from ${players}' },
        fakeGazeOnPlayersYou: { en: 'Face ${players}' },
        gazeOnPlayersYou: { en: 'Look Away from ${players}' },
      },
    },
    {
      id: 'DMU P4 Dynamic Fluid',
      type: 'GainsEffect',
      netRegex: { effectId: '15A7', capture: true },
      condition: (_pull, hit) => parseFloat(hit.duration) > 68,
      delaySeconds: (_pull, hit) => parseFloat(hit.duration),
      durationSeconds: 6.9,
      suppressSeconds: 99999,
      alertText: (pull, _hit, voice) => {
        const isFluidTru = pull.isFluidTrue;
        if (isFluidTru === undefined)
          return;

        return isFluidTru ? voice.donuts() : voice.puddles();
      },
      outputStrings: {
        donuts: { en: 'Stack for Donuts' },
        puddles: Voices.baitPuddles,
      },
    },
    {
      id: 'DMU P4 Mana Release',
      type: 'StartsUsing',
      netRegex: { id: 'BAA5', source: 'Kefka', capture: true },
      condition: (pull) => {
        return pull.isIceTrue !== undefined && pull.isThunderTrue !== undefined;
      },
      delaySeconds: (_pull, hit) => parseFloat(hit.castTime) + 0.3,
      infoText: (pull, _hit, voice) => {
        const isThunderChargeds = pull.isThunderChargedTrue;
        const isBlizzardChargeds = pull.isBlizzardChargedTrue;
        const isFluidTru = pull.isFluidTrue;
        const trueThunders = (isThunderChargeds && pull.isThunderTrue) ||
          (!isThunderChargeds && !pull.isThunderTrue);
        const trueIc = (isBlizzardChargeds && pull.isIceTrue) ||
          (!isBlizzardChargeds && !pull.isIceTrue);

        if (trueThunders) {
          const tell = trueIc
            ? voice.trueIceTrueThunder()
            : voice.fakeIceTrueThunder();
          return isFluidTru
            ? voice.tellsDonut({
              tells: tell,
              donut: voice.inDonut(),
            })
            : tell;
        }
        const tell = trueIc
          ? voice.trueIceFakeThunder()
          : voice.fakeIceFakeThunder();
        return isFluidTru
          ? voice.tellsDonut({
            tells: tell,
            donut: voice.inDonut(),
          })
          : tell;
      },
      outputStrings: Object.assign({}, mysteryMagicVoiceWords, {
        inDonut: { en: 'In Donut' },
        tellsDonut: { en: '${tells} + ${donut}' },
      }),
    },
    {
      id: 'DMU P4 Fake Stray Spray',
      type: 'GainsEffect',
      netRegex: { effectId: '15AC', capture: true },
      condition: (pull) => !pull.isFluidTrue,
      delaySeconds: (_pull, hit) => parseFloat(hit.duration),
      suppressSeconds: 99999,
      response: Response.moveAway('alert'),
    },
    {
      id: 'DMU P4/P5 Ultima Upsurge',
      type: 'StartsUsing',
      netRegex: { id: 'C24A', source: 'Kefka', capture: false },
      response: Response.bigAoe(),
    },
    {
      id: 'DMU P4 Enrage (Failed)',
      type: 'StartsUsing',
      netRegex: { id: 'BABB', capture: false },
      alarmText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: { en: 'Failed' },
      },
    },
    {
      id: 'DMU P5 Ultima Repeater',
      type: 'StartsUsing',
      netRegex: { id: 'BB40', capture: false },
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: { en: 'Raidwide x4' },
      },
    },
    {
      id: 'DMU P5 Flood',
      type: 'StartsUsing',
      netRegex: { id: 'C13F', capture: false },
      response: Response.aoe(),
    },
    {
      id: 'DMU P5 Flood Move',
      type: 'Ability',
      netRegex: { id: 'C269', capture: false },
      suppressSeconds: 0.5,
      response: Response.moveAway(),
    },
    {
      id: 'DMU P5 Maddening Orchestra',
      type: 'StartsUsing',
      netRegex: { id: 'BB50', capture: false },
      response: Response.spread(),
    },
    {
      id: 'DMU P5 Maddening Orchestra Flare',
      type: 'GainsEffect',
      netRegex: { effectId: '14E6', capture: true },
      run: (pull, hit) => pull.p5FlareTank = hit.target,
    },
    {
      id: 'DMU P5 Maddening Orchestra Flare on You',
      type: 'GainsEffect',
      netRegex: { effectId: '14E6', capture: true },
      condition: Condition.targetIsYou(),
      alertText: (_pull, _hit, voice) => voice.flare(),
      outputStrings: {
        flare: { en: 'Surprise Flare (get out)' },
      },
    },
    {
      id: 'DMU P5 Maddening Orchestra Holy on You',
      type: 'GainsEffect',
      netRegex: { effectId: '14E7', capture: true },
      condition: Condition.targetIsYou(),
      alertText: (_pull, _hit, voice) => voice.holy(),
      outputStrings: {
        holy: { en: 'Surprise Holy (get in)' },
      },
    },
    {
      id: 'DMU P5 Celestriad',
      type: 'StartsUsing',
      netRegex: { id: 'BB42', capture: false },
      infoText: (_pull, _hit, voice) => voice.text(),
      run: (pull) => umadCeleRestart(pull),
      outputStrings: {
        text: { en: 'Elemental Towers' },
      },
    },
    {
      id: 'DMU P5 Celestriad Tower Collect',
      type: 'AddedCombatant',
      netRegex: { npcBaseId: ['2015294', '2015295', '2015296'], capture: true },
      condition: (pull) => pull.phase === 'p5',
      run: (pull, hit) => {
        const bas = parseInt(hit.npcBaseId, 10);
        const elBit = UMAD_CELE_PILLAR[bas];
        if (!elBit) return;
        pull.p5CeleTowers = pull.p5CeleTowers ?? [];
        const id = hit.id;
        if (pull.p5CeleTowers.some((t) => t.id === id)) return;
        pull.p5CeleTowers.push({
          el: elBit,
          base: bas,
          x: parseFloat(hit.x),
          y: parseFloat(hit.y),
          id: id,
        });
      },
    },
    {
      id: 'DMU P5 Celestriad Debuff Collect',
      type: 'GainsEffect',
      netRegex: { effectId: ['B56', 'BB6', 'B57'], capture: true },
      condition: (pull, hit) => pull.phase === 'p5' && hit.target === pull.me,
      run: (pull, hit) => {
        const elBit = hit.effectId === 'B56' ? 'fire'
          : hit.effectId === 'BB6' ? 'lightning'
          : 'ice';
        pull.p5CeleExpiry = pull.p5CeleExpiry ?? {};
        pull.p5CeleExpiry[elBit] = Date.now() + parseFloat(hit.duration) * 1000;
      },
    },
    {
      id: 'DMU P5 Celestriad Soak Order',
      type: 'GainsEffect',
      netRegex: { effectId: ['B56', 'BB6', 'B57'], capture: true },
      condition: (pull, hit) => {
        return pull.phase === 'p5' && hit.target === pull.me && !pull.p5CeleCalled;
      },
      delaySeconds: 1,
      alertText: (pull, _hit, voice) => umadCeleSpeaks(pull, voice),
      outputStrings: umadCeleVoiceWords,
    },
    {
      id: 'DMU P5 Celestriad Soak Order (Towers)',
      type: 'StartsUsing',
      netRegex: { id: ['BB43', 'BB44', 'BB45'], capture: false },
      condition: (pull) => pull.phase === 'p5' && !pull.p5CeleCalled,
      delaySeconds: 0.3,
      alertText: (pull, _hit, voice) => umadCeleSpeaks(pull, voice),
      outputStrings: umadCeleVoiceWords,
    },
    {
      id: 'DMU P5 Celestriad Catastrophic Choice',
      type: 'StartsUsing',
      netRegex: { id: ['C24E', 'C24F'], capture: true },
      infoText: (_pull, hit, voice) => {
        return hit.id === 'C24E' ? voice.out() : voice.in();
      },
      outputStrings: {
        out: Voices.out,
        in: Voices.in,
      },
    },
    {
      id: 'DMU P5 Exaflares',
      type: 'StartsUsing',
      netRegex: { id: 'BB3B', capture: false },
      infoText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: { en: 'Exaflares' },
      },
    },
    {
      id: 'DMU P5 Stray Entropy',
      type: 'StartsUsing',
      netRegex: { id: 'BB3E', capture: false },
      response: Response.spread(),
    },
    {
      id: 'DMU P5 Forsaken',
      type: 'StartsUsing',
      netRegex: { id: 'BB35', capture: false },
      response: Response.bigAoe('alert'),
    },
    {
      id: 'DMU P5 Forsaken Stack',
      type: 'HeadMarker',
      netRegex: { id: headSignState['stompStack'], capture: true },
      condition: (pull) => pull.phase === 'p5',
      alertText: (pull, hit, voice) => {
        if (hit.target === pull.me)
          return voice.stackOnYou();
        return voice.stackOnPlayer({ player: pull.party.member(hit.target) });
      },
      outputStrings: {
        stackOnYou: Voices.stackOnYou,
        stackOnPlayer: Voices.stackOnPlayer,
      },
    },
    {
      id: 'DMU P5 Forsaken Move',
      type: 'StartsUsing',
      netRegex: { id: 'BB38', capture: false },
      suppressSeconds: 0.5,
      response: Response.moveAway(),
    },
    {
      id: 'DMU P5 Enrage',
      type: 'StartsUsing',
      netRegex: { id: 'BB3A', capture: false },
      alarmText: (_pull, _hit, voice) => voice.text(),
      outputStrings: {
        text: { en: 'Enrage' },
      },
    },
];
defineDuty({
  id: "DancingMadUltimate",
  name: "UMAD - Dancing Mad",
  category: "Ultimate",
  zoneId: 1363,
  boss: "Kefka",
  config: [
    {
      id: "teleportent",
      name: "P1: Arrows",
      default: "none",
      options: [
        { value: "clockwise", label: "Bigbox / Freaky" },
        { value: "filipino", label: "Filipino / Static" },
        { value: "none", label: "Call Debuffs only" }
      ]
    },
    {
      id: "forsaken",
      name: "P2: Forsaken",
      default: "none",
      options: [
        { value: "none", label: "Generic (3-4-1)" },
        { value: "buddy", label: "Buddy / Meow (EU)" },
        { value: "kroxy-rinon", label: "Kroxy-Rinon" }
      ]
    },
    {
      id: "boa",
      name: "P3: Bowels of Agony",
      default: "none",
      options: [
        { value: "none", label: "Generic Calls" },
        { value: "lb3", label: "Tank LB3" }
      ]
    },
    {
      id: "blackHole",
      name: "P3: Black Hole Order",
      default: "dsa",
      options: [
        { value: "dsa", label: "D>S>A" },
        { value: "sda", label: "S>D>A" },
        { value: "modified", label: "D>S>A Double Tether" },
        { value: "none", label: "Generic calls" }
      ]
    },
    {
      id: "blackHoleTether",
      name: "P3: Black Hole Tether Style",
      default: "true",
      options: [
        { value: "true", label: "True North" },
        { value: "clock", label: "Clockwise Number" }
      ]
    }
  ],
  state: {
    phase: "p1",
    triggerSetConfig: {
      teleportent: "none",
      forsaken: "none",
      blackHole: "dsa",
      blackHoleTether: "true",
      boa: "none"
    },
    actorPositions: {},
    gravenImageCount: 0,
    blueTowerIds: [],
    purpleTowerIds: [],
    yellowTowerIds: [],
    eyeTowerIds: [],
    fakeEyeTowerIds: [],
    waveCannonTargets: [],
    doubleTroubleTrapTargets: [],
    pathOfLightCounter: 1,
    myPathOfLights: [],
    pathOfLightStackPlayers: [],
    pathOfLightConePlayers: [],
    pathOfLightSpreadPlayers: [],
    pathOfLightMarkers: {},
    buddyGroup: "unknown",
    isForsakenGroupA: false,
    windCrystalNext: false,
    fireElementPlayers: [],
    waterElementPlayers: [],
    inLine: {},
    hadAccretion: false,
    blackHoleIdDirNums: {},
    nothingnessTracker: 1,
    blackHoleTetherDirNums: [],
    trineDirNums: [],
    grandCrossCount: 0,
    shortShriekPlayers: [],
    longShriekPlayers: [],
    shortForkedPlayers: [],
    longForkedPlayers: [],
    shortCompressedPlayers: [],
    longCompressedPlayers: [],
    firstShortBombPlayers: [],
    firstLongBombPlayers: [],
    secondShortBombPlayers: [],
    secondLongBombPlayers: [],
    lcCount: 0,
    lcFirstDir: 0,
    lcFirstX: 0,
    lcFirstZ: 0,
    lcArmed: false,
    kefkaId: undefined,
    isKnockDown2: false
  },
  mechanics: umadCues.map(function (t) { return raws(t); })
});
