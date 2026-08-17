
function enuoPileLabel(rtns) {
  if (rtns === 'healerStacks') return 'Healer Groups';
  if (rtns === 'stack') return 'Stack';
  return 'Stacks';
}

function enuoDir8s(n) {
  var o = Voices[Facings.output8Dir[n]] || Voices.unknown;
  return o.en;
}

function enuoDir16s(n) {
  var o = Voices[Facings.output16Dir[n]] || Voices.unknown;
  return o.en;
}

function enuoCollectNaughtGrow(ctxs) {
  var id = ctxs.m.id;
  var typ = (id === 'C339' || id === 'C33B') ? 'aoe' : 'donut';
  var facingTwo;
  if (ctxs.x !== undefined && Math.abs(ctxs.x - ctxs.cx) < 1 && Math.abs(ctxs.y - ctxs.cy) < 1)
    facingTwo = 'middle';
  else if (ctxs.x !== undefined)
    facingTwo = ctxs.dir8(ctxs.x, ctxs.y);
  else
    facingTwo = 'middle';
  if (!ctxs.data.ng) ctxs.data.ng = [];
  ctxs.data.ng.push({ dir: facingTwo, type: typ });
}

function enuoResolveNaughtGrow(ctxs) {
  var ngBit = ctxs.data.ng;
  if (!ngBit || ngBit.length === 0) return;
  ctxs.data.ng = [];
  var bit = enuoPileLabel(ctxs.data.rtn);

  var p1Bit = ngBit[0], p2Bit = ngBit[1];
  if (!p1Bit) return;

  if (!p2Bit) {
    if (p1Bit.type === 'aoe') return 'Away from ' + p1Bit.dir + ' + ' + bit;
    return p1Bit.dir + ' + ' + bit;
  }

  var mids = (p2Bit.dir === 'middle') ? p2Bit : p1Bit;
  var vdBit  = (p1Bit.dir === 'middle') ? p2Bit : p1Bit;
  if (mids.type === 'donut') return 'Under boss + away from ' + vdBit.dir + ' + ' + bit;
  return vdBit.dir + ' + away from boss + ' + bit;
}

function enuoCollectPassag(ctxs) {
  if (!ctxs.data.pon) ctxs.data.pon = [];
  var siz = ctxs.m.id === 'C341' ? 'big' : 'small';
  ctxs.data.pon.push({ x: ctxs.x, y: ctxs.y, heading: ctxs.heading, type: siz });
  ctxs.data.ponCount = (ctxs.data.ponCount || 0) + (siz === 'big' ? 2 : 1);
}

function enuoResolvePassag(ctxs) {
  if ((ctxs.data.ponCount || 0) < 4) return;
  var row = ctxs.data.pon || [];
  ctxs.data.pon = [];
  ctxs.data.ponCount = 0;

  var l1Bit = row[0], l2Bit = row[1], l3Bit = row[2], l4Bit = row[3];
  if (!l1Bit || !l2Bit) return;

  if (l3Bit === undefined) {
    return (Facings.hdgTo8DirNum(l1Bit.heading) % 2 === 0) ? 'Intercards' : 'Cardinals';
  }
  if (l4Bit !== undefined) return 'Get Middle';

  var bigs = l1Bit.type === 'big' ? l1Bit : (l2Bit.type === 'big' ? l2Bit : l3Bit);
  var bigDigit = Facings.xyTo8DirNum(bigs.x, bigs.y, ctxs.cx, ctxs.cy);
  var safe1s = (bigDigit + 2) % 8;
  var safe2s = (bigDigit + 6) % 8;
  var s1Bit = Math.min(safe1s, safe2s);
  var s2Bit = Math.max(safe1s, safe2s);
  return 'Go ' + enuoDir8s(s1Bit) + '/' + enuoDir8s(s2Bit) + ' max melee';
}

function enuoResolveGaz(ctxs) {
  var coneFacing = Facings.hdgTo8DirNum(ctxs.heading);
  var openFacing, closeFacing;
  if (ctxs.data.gazeDir === 'CCW') {
    openFacing = (coneFacing + 1) % 8;
    closeFacing = ((coneFacing + 8) - 2) % 8;
  } else {
    openFacing = ((coneFacing + 8) - 1) % 8;
    closeFacing = (coneFacing + 2) % 8;
  }
  var rots = ctxs.data.gazeDir === 'CCW' ? 'Counter-Clockwise' : 'Clockwise';
  return enuoDir8s(openFacing) + ' ' + rots + ' => ' + enuoDir8s(closeFacing);
}

function enuoCollectFlar(ctxs) {
  if (!ctxs.data.flare) ctxs.data.flare = [];
  ctxs.data.flare.push(ctxs.m.target);
}

function enuoResolveFlar(ctxs) {
  var f = ctxs.data.flare || [];
  if (f.length < 2) return;
  ctxs.data.flare = [];
  if (f.indexOf(ctxs.me) >= 0) return 'Tank flare on YOU => keep moving';
  return 'Away from tank flares => keep moving';
}

defineDuty({
  id: "TheUnmakingExtreme",
  name: "The Unmaking (Extreme)",
  category: "Extreme",
  zoneId: 1362,
  boss: "Enuo",
  center: { x: 100, y: 100 },
  state: { ng: [], rtn: 'unknown', pon: [], ponCount: 0, gazeDir: 'CW', flare: [] },
  mechanics: [
    whenChant("C381").aoe("Meteorain"),
    whenChant("C334").aoe("Almagest"),
    whenChant("C382").aoe("Almagest enrage"),
    whenChant("C36D").bigAoe("Lightless World"),

    whenSign("02BD").track(function (ctxs) { ctxs.data.rtn = 'healerStacks'; }),
    whenSign("02BE").track(function (ctxs) { ctxs.data.rtn = 'stack'; }),
    whenChantExtra(["C339", "C33A", "C33B", "C33C"])
      .collect(enuoCollectNaughtGrow)
      .after(0.5).hold(6)
      .alert().resolve(enuoResolveNaughtGrow),

    whenChant("C378").alert("Bait puddles \u2192 stop \u2192 spread"),

    whenChant("C370").stack("Partner stacks"),
    whenChant("C371").stack("Healer groups"),
    whenChant("C37D").stack("Healer groups"),
    whenChant("C37F").stack("Line stack"),

    whenChant("C353").track(function (ctxs) { ctxs.data.gazeDir = 'CW'; }),
    whenChant("C354").track(function (ctxs) { ctxs.data.gazeDir = 'CCW'; }),
    whenChantExtra("C355").after(0.2).hold(9).cooldown(20)
      .alert().resolve(enuoResolveGaz),

    whenChantExtra("C34B").after(0.2).hold(6).cooldown(2)
      .info().resolve(function (ctxs) {
        var dangers = Facings.xyTo16DirNum(ctxs.x, ctxs.y, ctxs.cx, ctxs.cy);
        return enuoDir16s((dangers + 9) % 16) + ' close';
      }),

    whenChantExtra(["C341", "C342", "C343"])
      .collect(enuoCollectPassag)
      .after(0.3).hold(8)
      .alert().resolve(enuoResolvePassag),

    whenChant("C37C")
      .collect(enuoCollectFlar)
      .after(0.1).hold(6)
      .alert().resolve(enuoResolveFlar),
    whenChant("C37B").info("Tank flares \u2192 keep moving").cooldown(1),

    whenChant("C365").by("Protective Shadow").onYou().tankbuster("Tank cleave on YOU"),
    whenChant("C362").by("Protective Shadow").onTank().alert("Interrupt Drain Touch").cooldown(1),
    whenSkill("C369").by("Soothing Shadow").onYou().info("Cleanse debuff"),
    whenChant("C366").by("Aggressive Shadow").alert("Look toward middle").cooldown(1),

    whenChant("C33E").by("Looming Shadow").knockback("Knockback"),

    whenSign("02D1").onYou().alert("Cone on YOU"),
    whenLeash(["0194", "0195"]).onYou().alert("Chasing puddle on YOU \u2192 run it out")
  ]
});
