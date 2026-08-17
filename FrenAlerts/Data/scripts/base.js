
globalThis.triggerSets = [];

function __formatTemplate(tmpls, param) {
  if (tmpls === undefined || tmpls === null) return '';
  return String(tmpls).replace(/\$\{(\w+)\}/g, function (_, k) {
    return param && param[k] !== undefined && param[k] !== null ? String(param[k]) : '';
  });
}

function __resolveString(tmpls, langs) {
  if (tmpls && typeof tmpls === 'object')
    return tmpls[langs] !== undefined ? tmpls[langs] : tmpls['en'];
  return tmpls;
}

function makeOutput(staticWords, langs, cueCode) {
  var words = Object.assign({}, staticWords || {});
  return new Proxy({}, {
    get: function (_c, tagTwo) {
      if (tagTwo === 'responseOutputStrings') return words;
      if (typeof tagTwo !== 'string') return undefined;
      return function (param) {
        var defs = __formatTemplate(__resolveString(words[tagTwo], langs), param);
        if (!cueCode || typeof __ov !== 'function') return defs;
        var mod = (typeof __ovMode === 'function') ? __ovMode() : 'text';
        var own = __ov(cueCode, tagTwo, mod);
        if (own === undefined || own === null || own === '') return defs;
        own = String(own).split('{default}').join(defs);
        return __formatTemplate(own, param);
      };
    },
    set: function (_c, tagTwo, vals) {
      if (tagTwo === 'responseOutputStrings') words = Object.assign({}, words, vals);
      else words[tagTwo] = vals;
      return true;
    },
  });
}

var Voices = {
  aoe: { en: 'AoE' },
  bigAoe: { en: 'big AoE!' },
  bleedAoe: { en: 'AoE + Bleed' },
  hpTo1Aoe: { en: 'HP to 1' },
  tankBuster: { en: 'Tank Buster' },
  miniBuster: { en: 'Mini Buster' },
  tankBusterOnPlayer: { en: 'Tank Buster on ${player}' },
  tankBusterOnYou: { en: 'Tank Buster on YOU' },
  tankBusters: { en: 'Tank Busters' },
  tetherBusters: { en: 'Tank Tethers' },
  avoidTetherBusters: { en: 'Avoid Tank Tethers' },
  tankCleave: { en: 'Tank Cleave' },
  tankBusterCleaves: { en: 'Tank Buster Cleaves' },
  tankBusterCleavesOnYou: { en: 'Tank Cleaves on YOU' },
  avoidTankCleave: { en: 'Avoid Tank Cleave' },
  avoidTankCleaves: { en: 'Avoid Tank Cleaves' },
  tankCleaveOnYou: { en: 'Tank Cleave on YOU' },
  sharedTankbuster: { en: 'Shared Tank Buster' },
  sharedTankbusterOnYou: { en: 'Shared Tank Buster on YOU' },
  sharedTankbusterOnPlayer: { en: 'Shared Tank Buster on ${player}' },
  tankSwap: { en: 'Tank Swap!' },
  spread: { en: 'Spread' },
  defamationOnYou: { en: 'Defamation on YOU' },
  protean: { en: 'Protean' },
  stackMarker: { en: 'Stack' },
  getTogether: { en: 'Stack' },
  healerGroups: { en: 'Healer Groups' },
  rolePositions: { en: 'Role Positions' },
  stackOnYou: { en: 'Stack on YOU' },
  stackOnPlayer: { en: 'Stack on ${player}' },
  stackPartner: { en: 'Stack With Partner' },
  stackMiddle: { en: 'Stack in Middle' },
  stackInTower: { en: 'Stack in Tower' },
  baitPuddles: { en: 'Bait Puddles' },
  stacks: { en: 'Stacks' },
  doritoStack: { en: 'Dorito Stack' },
  spreadThenStack: { en: 'Spread => Stack' },
  stackThenSpread: { en: 'Stack => Spread' },
  drawIn: { en: 'Draw In' },
  knockback: { en: 'Knockback' },
  knockbackOnYou: { en: 'Knockback on YOU' },
  knockbackOnPlayer: { en: 'Knockback on ${player}' },
  lookTowardsBoss: { en: 'Look Towards Boss' },
  lookAway: { en: 'Look Away' },
  lookAwayFromPlayer: { en: 'Look Away from ${player}' },
  lookAwayFromTarget: { en: 'Look Away from ${name}' },
  getBehind: { en: 'Get Behind' },
  goFrontOrSides: { en: 'Go Front / Sides' },
  goFront: { en: 'Go Front' },
  getUnder: { en: 'Get Under' },
  in: { en: 'In' },
  out: { en: 'Out' },
  outOfMelee: { en: 'Out of Melee' },
  outOfHitbox: { en: 'Out of Hitbox' },
  inThenOut: { en: 'In => Out' },
  outThenIn: { en: 'Out => In' },
  backThenFront: { en: 'Back => Front' },
  frontThenBack: { en: 'Front => Back' },
  sidesThenFrontBack: { en: 'Sides => Front/Back' },
  frontBackThenSides: { en: 'Front/Back => Sides' },
  goIntoMiddle: { en: 'Get Middle' },
  front: { en: 'Front' },
  back: { en: 'Back' },
  right: { en: 'Right' },
  rightEast: { en: 'Right/East' },
  left: { en: 'Left' },
  leftWest: { en: 'Left/West' },
  getLeftAndWest: { en: '<= Get Left/West' },
  getRightAndEast: { en: 'Get Right/East =>' },
  leftThenRight: { en: 'Left => Right' },
  rightThenLeft: { en: 'Right => Left' },
  goFrontBack: { en: 'Go Front/Back' },
  sides: { en: 'Sides' },
  middle: { en: 'Middle' },
  clockwise: { en: 'Clockwise' },
  counterclockwise: { en: 'Counter-Clockwise' },
  killAdds: { en: 'Kill Adds' },
  killExtraAdd: { en: 'Kill Extra Add' },
  awayFromFront: { en: 'Away From Front' },
  preyOnYou: { en: 'Prey on YOU' },
  preyOnPlayer: { en: 'Prey on ${player}' },
  awayFromGroup: { en: 'Away from Group' },
  awayFromPlayer: { en: 'Away from ${player}' },
  meteorOnYou: { en: 'Meteor on YOU' },
  stopMoving: { en: 'Stop Moving!' },
  stopEverything: { en: 'Stop Everything!' },
  moveAway: { en: 'Move!' },
  moveAround: { en: 'Move!' },
  breakChains: { en: 'Break Chains' },
  moveChainsTogether: { en: 'Move Chains Together' },
  earthshakerOnYou: { en: 'Earth Shaker on YOU' },
  wakeUp: { en: 'WAKE UP' },
  getTowers: { en: 'Get Towers' },
  unknown: { en: '???' },
  cardinals: { en: 'Cardinals' },
  intercards: { en: 'Intercards' },
  north: { en: 'North' },
  south: { en: 'South' },
  east: { en: 'East' },
  west: { en: 'West' },
  northwest: { en: 'Northwest' },
  northeast: { en: 'Northeast' },
  southwest: { en: 'Southwest' },
  southeast: { en: 'Southeast' },
  dirN: { en: 'N' }, dirS: { en: 'S' }, dirE: { en: 'E' }, dirW: { en: 'W' },
  dirNW: { en: 'NW' }, dirNE: { en: 'NE' }, dirSW: { en: 'SW' }, dirSE: { en: 'SE' },
  dirNNE: { en: 'NNE' }, dirENE: { en: 'ENE' }, dirESE: { en: 'ESE' }, dirSSE: { en: 'SSE' },
  dirSSW: { en: 'SSW' }, dirWSW: { en: 'WSW' }, dirWNW: { en: 'WNW' }, dirNNW: { en: 'NNW' },
  tank: { en: 'Tank' }, healer: { en: 'Healer' }, dps: { en: 'DPS' },
  next: { en: ' => ' }, and: { en: ' + ' }, or: { en: ' / ' },
  num0: { en: '0' }, num1: { en: '1' }, num2: { en: '2' }, num3: { en: '3' }, num4: { en: '4' },
  num5: { en: '5' }, num6: { en: '6' }, num7: { en: '7' }, num8: { en: '8' }, num9: { en: '9' },
  goLeft: { en: 'Left' }, goRight: { en: 'Right' },
};

function __sevKey(sevs, defs) { return sevs ? sevs + 'Text' : defs; }
function __staticResp(defs, awayTwo) {
  return function (sevs) {
    return function (_pull, _hit, voice) {
      voice.responseOutputStrings = { text: awayTwo };
      var r = {}; r[__sevKey(sevs, defs)] = voice.text(); return r;
    };
  };
}

var Response = {
  aoe: __staticResp('infoText', Voices.aoe),
  bigAoe: __staticResp('infoText', Voices.bigAoe),
  hpTo1Aoe: __staticResp('infoText', Voices.hpTo1Aoe),
  tankBuster: __staticResp('alertText', Voices.tankBuster),
  spread: __staticResp('infoText', Voices.spread),
  getTogether: __staticResp('alertText', Voices.getTogether),
  stackMarker: __staticResp('alertText', Voices.stackMarker),
  getOut: __staticResp('alertText', Voices.out),
  getIn: __staticResp('alertText', Voices.in),
  getUnder: __staticResp('alertText', Voices.getUnder),
  getBehind: __staticResp('alertText', Voices.getBehind),
  goMiddle: __staticResp('alertText', Voices.goIntoMiddle),
  goSides: __staticResp('alertText', Voices.sides),
  goRight: __staticResp('alertText', Voices.right),
  goLeft: __staticResp('alertText', Voices.left),
  goWest: __staticResp('alertText', Voices.getLeftAndWest),
  goEast: __staticResp('alertText', Voices.getRightAndEast),
  goFrontBack: __staticResp('alertText', Voices.goFrontBack),
  moveAway: __staticResp('infoText', Voices.moveAway),
  moveAround: __staticResp('infoText', Voices.moveAround),
  breakChains: __staticResp('infoText', Voices.breakChains),
  knockback: __staticResp('infoText', Voices.knockback),
  stopMoving: __staticResp('alarmText', Voices.stopMoving),
  stopEverything: __staticResp('alarmText', Voices.stopEverything),
  meteorOnYou: __staticResp('alarmText', Voices.meteorOnYou),
  getTowers: __staticResp('infoText', Voices.getTowers),
  killAdds: __staticResp('infoText', Voices.killAdds),
  awayFromFront: __staticResp('alertText', Voices.awayFromFront),
  lookAway: __staticResp('alertText', Voices.lookAway),
  outOfMelee: __staticResp('infoText', Voices.outOfMelee),
  drawIn: __staticResp('alertText', Voices.drawIn),
  earthshaker: function (sevs) {
    return function (pull, hit, voice) {
      voice.responseOutputStrings = { earthshaker: Voices.earthshakerOnYou };
      var r = {};
      r[__sevKey(sevs, 'alertText')] = function () { return voice.earthshaker(); };
      return r;
    };
  },
  stackMarkerOn: function (sevs) {
    return function (pull, hit, voice) {
      voice.responseOutputStrings = { stackOnYou: Voices.stackOnYou, stackMarker: Voices.stackMarker };
      var r = {};
      r[__sevKey(sevs, 'alertText')] = (hit && hit.target === pull.me)
        ? voice.stackOnYou() : voice.stackMarker();
      return r;
    };
  },
  sharedTankBuster: function (markSev, otherSevs) {
    return function (pull, hit, voice) {
      voice.responseOutputStrings = {
        onYou: Voices.sharedTankbusterOnYou,
        shared: Voices.sharedTankbuster,
        avoid: Voices.avoidTankCleave,
      };
      var r = {};
      if (hit && hit.target === pull.me)
        r[__sevKey(markSev, 'alertText')] = voice.onYou();
      else
        r[__sevKey(otherSevs, 'infoText')] = voice.avoid();
      return r;
    };
  },
  tankBusterSwap: function (busterSevs, tradeSev) {
    return function (pull, hit, voice) {
      voice.responseOutputStrings = {
        noTarget: Voices.tankBuster,
        tankSwap: Voices.tankSwap,
        busterOnYou: Voices.tankBusterOnYou,
        busterOnTarget: Voices.tankBusterOnPlayer,
      };
      var r = {};
      var markThree = hit && hit.target;
      if (pull.role === 'tank' && markThree !== pull.me)
        r[__sevKey(tradeSev, 'alertText')] = voice.tankSwap();
      if (pull.role === 'tank' && markThree !== pull.me)
        return r;
      if (markThree === pull.me)
        r[__sevKey(busterSevs, 'alertText')] = voice.busterOnYou();
      else if (!markThree)
        r[__sevKey(busterSevs, 'alertText')] = voice.noTarget();
      else
        r[__sevKey(busterSevs, 'alertText')] = voice.busterOnTarget({ player: pull.party.member(markThree) });
      return r;
    };
  },
  tankCleave: function (sevs) {
    return function (pull, hit, voice) {
      voice.responseOutputStrings = {
        cleaveOnYou: Voices.tankCleaveOnYou,
        cleaveNoTarget: Voices.tankCleave,
        avoidCleave: Voices.avoidTankCleave,
      };
      var r = {};
      var markThree = hit && hit.target;
      r[__sevKey(sevs, 'infoText')] = function (d, m, o) {
        if (markThree === d.me) return o.cleaveOnYou();
        if (d.role === 'tank' || d.job === 'BLU') return o.cleaveNoTarget();
        return o.avoidCleave();
      };
      return r;
    };
  },
};

var __output8Dir = ['dirN', 'dirNE', 'dirE', 'dirSE', 'dirS', 'dirSW', 'dirW', 'dirNW'];
var __output16Dir = [
  'dirN', 'dirNNE', 'dirNE', 'dirENE', 'dirE', 'dirESE', 'dirSE', 'dirSSE',
  'dirS', 'dirSSW', 'dirSW', 'dirWSW', 'dirW', 'dirWNW', 'dirNW', 'dirNNW',
];
var __outputCardinalDir = ['dirN', 'dirE', 'dirS', 'dirW'];
var __outputIntercardDir = ['dirNE', 'dirSE', 'dirSW', 'dirNW'];
function __dirStrings(tags) {
  var o = {};
  for (var i = 0; i < tags.length; i++) o[tags[i]] = Voices[tags[i]];
  o.unknown = Voices.unknown;
  return o;
}
var Facings = {
  output8Dir: __output8Dir,
  output16Dir: __output16Dir,
  outputCardinalDir: __outputCardinalDir,
  outputIntercardDir: __outputIntercardDir,
  outputStrings16Dir: __dirStrings(__output16Dir),
  outputStrings8Dir: __dirStrings(__output8Dir),
  outputStringsCardinalDir: __dirStrings(__outputCardinalDir),
  outputStringsIntercardDir: __dirStrings(__outputIntercardDir),
  compareDirectionOutput: function (a, b) {
    var iaBit = __output16Dir.indexOf(a); if (iaBit < 0) iaBit = __output16Dir.length;
    var ibBit = __output16Dir.indexOf(b); if (ibBit < 0) ibBit = __output16Dir.length;
    return iaBit - ibBit;
  },
  xyTo16DirNum: function (x, y, cxBit, cyBit) {
    x = x - cxBit; y = y - cyBit;
    return (Math.round(8 - 8 * Math.atan2(x, y) / Math.PI) % 16 + 16) % 16;
  },
  xyTo8DirNum: function (x, y, cxBit, cyBit) {
    x = x - cxBit; y = y - cyBit;
    return (Math.round(4 - 4 * Math.atan2(x, y) / Math.PI) % 8 + 8) % 8;
  },
  xyTo4DirNum: function (x, y, cxBit, cyBit) {
    x = x - cxBit; y = y - cyBit;
    return (Math.round(2 - 2 * Math.atan2(x, y) / Math.PI) % 4 + 4) % 4;
  },
  xyTo4DirIntercardNum: function (x, y, cxBit, cyBit) {
    x = x - cxBit; y = y - cyBit;
    return (Math.round(2 - 2 * ((Math.PI / 4) + Math.atan2(x, y)) / Math.PI) % 4 + 4) % 4;
  },
  hdgTo16DirNum: function (h) { return (Math.round(8 - 8 * h / Math.PI) % 16 + 16) % 16; },
  hdgTo8DirNum: function (h) { return (Math.round(4 - 4 * h / Math.PI) % 8 + 8) % 8; },
  hdgTo4DirNum: function (h) { return (Math.round(2 - h * 2 / Math.PI) % 4 + 4) % 4; },
  outputFrom8DirNum: function (n) { return __output8Dir[n] || 'unknown'; },
  outputFrom16DirNum: function (n) { return __output16Dir[n] || 'unknown'; },
  outputFromCardinalNum: function (n) { return __outputCardinalDir[n] || 'unknown'; },
  outputFromIntercardNum: function (n) { return __outputIntercardDir[n] || 'unknown'; },
  xyTo8DirOutput: function (x, y, cxBit, cyBit) { return __output8Dir[Facings.xyTo8DirNum(x, y, cxBit, cyBit)] || 'unknown'; },
  xyTo16DirOutput: function (x, y, cxBit, cyBit) { return __output16Dir[Facings.xyTo16DirNum(x, y, cxBit, cyBit)] || 'unknown'; },
  xyToCardinalDirOutput: function (x, y, cxBit, cyBit) { return __outputCardinalDir[Facings.xyTo4DirNum(x, y, cxBit, cyBit)] || 'unknown'; },
  xyToIntercardDirOutput: function (x, y, cxBit, cyBit) { return __outputIntercardDir[Facings.xyTo4DirIntercardNum(x, y, cxBit, cyBit)] || 'unknown'; },
};

var Condition = {
  targetIsYou: function () { return function (pull, hit) { return hit.target === pull.me; }; },
  targetIsNotYou: function () { return function (pull, hit) { return hit.target !== pull.me; }; },
  caster: function () { return function (pull, hit) { return hit.source === pull.me; }; },
  caresAboutPhysical: function () {
    return function (pull) {
      return pull.role === 'tank' || pull.role === 'healer' || pull.job === 'BLU';
    };
  },
};

function __initData() {
  return {
    phase: 'doorboss',
    mortalSlayerGreenLeft: 0,
    mortalSlayerGreenRight: 0,
    inLine: {},
    blobTowerDirs: [],
    skinsplitterCount: 0,
    cellChainCount: 0,
    hasRot: false,
    triggerSetConfig: { uptimeKnockbackStrat: false },
  };
}

function __newObj() { return {}; }

function __newArr() { return []; }

function bodiesByBase(csvs) {
  var rowTwo = (typeof __actorsByBase === 'function') ? __actorsByBase(csvs) : [];
  var awayTwo = [];
  for (var i = 0; i < rowTwo.length; i++)
    awayTwo.push({ base: rowTwo[i][0], x: rowTwo[i][1], y: rowTwo[i][2], heading: rowTwo[i][3] });
  return awayTwo;
}

function bodiesAll() {
  try {
    if (typeof __actorsAllInfo !== 'function') return [];
    return JSON.parse(__actorsAllInfo());
  } catch (e) { return []; }
}

function UnreachableCod() { return undefined; }
function sayOverlayHandler() { return { combatants: [] }; }

function __consoleSink(arg) {
  if (typeof __log !== 'function') return;
  var part = [];
  for (var i = 0; i < arg.length; i++) {
    var a = arg[i];
    try { part.push(typeof a === 'object' ? JSON.stringify(a) : String(a)); }
    catch (e) { part.push(String(a)); }
  }
  try { __log(part.join(' ')); } catch (e) {}
}
var consol = {
  log:   function () { __consoleSink(arguments); },
  info:  function () { __consoleSink(arguments); },
  warn:  function () { __consoleSink(arguments); },
  error: function () { __consoleSink(arguments); },
  debug: function () { __consoleSink(arguments); },
  assert: function () {},
};

function __merge(dsts, srcs) {
  if (srcs) { for (var k in srcs) dsts[k] = srcs[k]; }
  return dsts;
}

function __makeParty() {
  function rosters() {
    if (typeof __partyRoster !== 'function') return [];
    try { return JSON.parse(__partyRoster()); } catch (e) { return []; }
  }
  var buddySlots = { MT: 'H1', H1: 'MT', OT: 'H2', H2: 'OT', M1: 'R1', R1: 'M1', M2: 'R2', R2: 'M2' };
  var p = {
    member: function (n) { return __partyMember(n); },
    isDPS: function (n) { return __partyIsDPS(n); },
    jobName: function (n) {
      var r = rosters();
      for (var i = 0; i < r.length; i++) if (r[i].name === n) return r[i].job;
      return undefined;
    },
    roleSlot: function (n) { return (typeof __roleSlot === 'function') ? __roleSlot(n) : ''; },
    buddy: function (n) {
      if (typeof __manualRoleSlot !== 'function' || typeof __manualRoleName !== 'function') return '';
      var slots = __manualRoleSlot(n);
      if (!slots || !buddySlots[slots]) return '';
      return __manualRoleName(buddySlots[slots]) || '';
    },
  };
  Object.defineProperty(p, 'partyNames', {
    get: function () { return rosters().map(function (m) { return m.name; }); },
  });
  Object.defineProperty(p, 'details', {
    get: function () { return rosters(); },
  });
  return p;
}

function __makeOptions() {
  return { Debug: false };
}
