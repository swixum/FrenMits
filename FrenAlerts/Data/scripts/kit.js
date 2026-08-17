(function (globals) {

  function sevFields(sevs) {
    return sevs === 'alarm' ? 'alarmText'
         : sevs === 'info'  ? 'infoText'
         : 'alertText';
  }

  function isAct(v) { return typeof v === 'function'; }

  function num(v) {
    if (v === undefined || v === null || v === '') return undefined;
    var n = parseFloat(v);
    return isNaN(n) ? undefined : n;
  }

  function buildCtx(pull, hit, cfgs) {
    var centers = (cfgs && cfgs.center) || { x: 100, y: 100 };
    var cxBit = centers.x, cyBit = centers.y;

    var place = (pull.actorPositions && hit.sourceId)
      ? pull.actorPositions[hit.sourceId] : undefined;
    var x = num(hit.x); if (x === undefined && place) x = place.x;
    var y = num(hit.y); if (y === undefined && place) y = place.y;
    var hdgs = num(hit.heading); if (hdgs === undefined && place) hdgs = place.heading;

    function labels(tagTwo) {
      var o = (typeof Voices !== 'undefined') ? Voices[tagTwo] : null;
      return (o && o.en) ? o.en : String(tagTwo).replace(/^dir/, '');
    }

    return {
      data: pull,
      m: hit,
      matches: hit,
      me: pull.me,
      role: pull.role,

      x: x, y: y, heading: hdgs, cx: cxBit, cy: cyBit,

      isYou: function (n) { return n === pull.me; },
      youAreTarget: hit.target === pull.me,
      youAreSource: hit.source === pull.me,
      member: function (n) {
        return (pull.party && pull.party.member) ? pull.party.member(n) : n;
      },

      dir8: function (pxBit, pyBit) {
        return labels(Facings.xyTo8DirOutput(pxBit === undefined ? x : pxBit, pyBit === undefined ? y : pyBit, cxBit, cyBit));
      },
      dir16: function (pxBit, pyBit) {
        return labels(Facings.xyTo16DirOutput(pxBit === undefined ? x : pxBit, pyBit === undefined ? y : pyBit, cxBit, cyBit));
      },
      cardinal: function (pxBit, pyBit) {
        return labels(Facings.xyToCardinalDirOutput(pxBit === undefined ? x : pxBit, pyBit === undefined ? y : pyBit, cxBit, cyBit));
      },
      intercard: function (pxBit, pyBit) {
        return labels(Facings.xyToIntercardDirOutput(pxBit === undefined ? x : pxBit, pyBit === undefined ? y : pyBit, cxBit, cyBit));
      },
      side: function (pxBit) { return ((pxBit === undefined ? x : pxBit) < cxBit) ? 'West' : 'East'; },
      northSouth: function (pyBit) { return ((pyBit === undefined ? y : pyBit) < cyBit) ? 'North' : 'South'; },

      actors: function (csvs) { return bodiesByBase(csvs); },
      count: function (csvs) { return bodiesByBase(csvs).length; },

      pos: function (id) { return pull.actorPositions ? pull.actorPositions[id] : undefined; },
    };
  }

  function mechanicsTwo(typ, nets) {
    var specs = {
      type: typ,
      net: nets || {},
      conds: [],
      delay: 0,
      duration: 0,
      suppress: 0,
      sev: 'alert',
      text: null,
      ttsText: null,
      runFn: null,
      preFn: null,
      id: null,
      name: null,
      group: null,
    };

    var ap = {};
    ap.__spec = specs;

    ap.id        = function (v)    { specs.id = v; return ap; };
    ap.label     = function (v)    { specs.name = v; return ap; };
    ap.group     = function (v)    { specs.group = v; return ap; };
    ap.by        = function (labelTwo) { specs.net.source = labelTwo; return ap; };
    ap.from      = ap.by;
    ap.target    = function (labelTwo) { specs.net.target = labelTwo; return ap; };
    ap.onYou     = function ()     { specs.conds.push(function (c) { return c.m.target === c.me; }); return ap; };
    ap.bySource  = function (labelTwo) { specs.conds.push(function (c) { return c.m.source === labelTwo; }); return ap; };
    ap.onTank    = function ()     { specs.conds.push(function (c) { return c.role === 'tank'; }); return ap; };
    ap.onHealer  = function ()     { specs.conds.push(function (c) { return c.role === 'healer'; }); return ap; };
    ap.onDps     = function ()     { specs.conds.push(function (c) { return c.role === 'dps'; }); return ap; };
    ap.when      = function (act)   { if (isAct(act)) specs.conds.push(act); return ap; };

    ap.after     = function (secs)  { specs.delay = secs; return ap; };
    ap.hold      = function (secs)  { specs.duration = secs; return ap; };
    ap.cooldown  = function (secs)  { specs.suppress = secs; return ap; };
    ap.onceEvery = ap.cooldown;

    ap.track     = function (act)   { specs.runFn = act; return ap; };
    ap.collect   = function (act)   { specs.preFn = act; return ap; };

    function sevTerms(sevs) { return function (t) { specs.sev = sevs; specs.text = (t === undefined ? specs.text : t); return ap; }; }
    ap.alarm   = sevTerms('alarm');
    ap.danger  = ap.alarm;
    ap.alert   = sevTerms('alert');
    ap.say     = ap.alert;
    ap.info    = sevTerms('info');
    ap.notice  = ap.info;
    ap.resolve = function (act) { specs.text = act; if (specs.sev === undefined) specs.sev = 'alert'; return ap; };
    ap.tts     = function (t)  { specs.ttsText = t; return ap; };
    ap.silent  = function ()   { specs.ttsText = ''; return ap; };

    function presets(sevs, defs) { return function (t) { specs.sev = sevs; specs.text = (t === undefined ? defs : t); return ap; }; }
    // Raidwide rather than "AoE", matching Voices in base.js.
    ap.aoe         = presets('info',  'Raidwide');
    ap.bigAoe      = presets('info',  'Big Raidwide');
    ap.tankbuster  = presets('alert', 'Tank Buster');
    ap.tankbusterOnYou = function () { specs.conds.push(function (c) { return c.m.target === c.me; }); specs.sev = 'alert'; specs.text = 'Tank Buster on YOU'; return ap; };
    ap.stack       = presets('alert', 'Stack');
    ap.spread      = presets('info',  'Spread');
    ap.out         = presets('alert', 'Out');
    ap.getOut      = ap.out;
    ap['in']       = presets('alert', 'In');
    ap.getIn       = ap['in'];
    ap.under       = presets('alert', 'Get Under');
    ap.getUnder    = ap.under;
    ap.behind      = presets('alert', 'Get Behind');
    ap.getBehind   = ap.behind;
    ap.knockback   = presets('info',  'Knockback');
    ap.towers      = presets('info',  'Get Towers');
    ap.getTowers   = ap.towers;
    ap.lookAway    = presets('alert', 'Look Away');
    ap.stopMoving  = presets('alarm', 'Stop Moving!');
    ap.goLeft      = presets('alert', 'Left');
    ap.goRight     = presets('alert', 'Right');
    ap.goMiddle    = presets('alert', 'Get Middle');
    ap.goSides     = presets('alert', 'Sides');
    ap.goFront     = presets('alert', 'Go Front');
    ap.goBack      = presets('alert', 'Go Back');
    ap.protean     = presets('info',  'Protean / Spread');
    ap.baitPuddles = presets('alert', 'Bait Puddles');
    ap.breakTether = presets('info',  'Break Tether');
    ap.killAdds    = presets('info',  'Kill Adds');

    return ap;
  }

  function whenChant(id)        { return mechanicsTwo('StartsUsing', { id: id, capture: true }); }
  function whenChantExtra(id)   { return mechanicsTwo('StartsUsingExtra', { id: id, capture: true }); }
  function whenSkill(id)     { return mechanicsTwo('Ability', { id: id, capture: true }); }
  function whenSign(id)  { return mechanicsTwo('HeadMarker', { id: id, capture: true }); }
  function whenAura(effs)     { return mechanicsTwo('GainsEffect', { effectId: effs, capture: true }); }
  function whenAuraLoss(effs) { return mechanicsTwo('LosesEffect', { effectId: effs, capture: true }); }
  function whenLeash(id)      { return mechanicsTwo('Tether', { id: id, capture: true }); }
  function whenPush(npcBaseCode)  { return mechanicsTwo('AddedCombatant', { npcBaseId: npcBaseCode, capture: true }); }
  function whenBodyControl(picks) { return mechanicsTwo('ActorControlExtra', picks || {}); }
  function whenBody(picks)    { return mechanicsTwo('CombatantMemory', picks || {}); }

  function whenTwo(typ, nets) { return mechanicsTwo(typ, nets || {}); }

  function raws(cue) { return { __raw: cue || {} }; }

  function buildCue(ap, cfgs, spotTwo) {
    var s = ap.__spec;

    if (s.net.source === undefined && cfgs.boss &&
        (s.type === 'StartsUsing' || s.type === 'Ability')) {
      s.net.source = cfgs.boss;
    }

    var tagsTwo = s.net.id || s.net.effectId || s.net.npcBaseId || ('#' + spotTwo);
    if (Array.isArray(tagsTwo)) tagsTwo = tagsTwo.join('/');
    var id = s.id || ((cfgs.id || cfgs.name) + ' ' + s.type + ' ' + tagsTwo + ' [' + spotTwo + ']');

    var trigs = { id: id, type: s.type, netRegex: s.net };
    if (s.name)  trigs.name  = s.name;
    if (s.group) trigs.group = s.group;

    if (s.conds.length > 0) {
      trigs.condition = function (pull, hit) {
        var c = buildCtx(pull, hit, cfgs);
        for (var i = 0; i < s.conds.length; i++) {
          if (!s.conds[i](c)) return false;
        }
        return true;
      };
    }

    if (s.delay > 0)    trigs.delaySeconds = s.delay;
    if (s.duration > 0) trigs.durationSeconds = s.duration;
    if (s.suppress > 0) trigs.suppressSeconds = s.suppress;

    if (isAct(s.preFn)) trigs.preRun = function (pull, hit) { s.preFn(buildCtx(pull, hit, cfgs)); };
    if (isAct(s.runFn)) trigs.run    = function (pull, hit) { s.runFn(buildCtx(pull, hit, cfgs)); };

    if (s.text !== null) {
      trigs[sevFields(s.sev)] = function (pull, hit) {
        var c = buildCtx(pull, hit, cfgs);
        var t = isAct(s.text) ? s.text(c) : s.text;
        return (t === undefined || t === null || t === '') ? undefined : t;
      };
    }

    if (s.ttsText !== null) {
      trigs.tts = function (pull, hit) {
        var c = buildCtx(pull, hit, cfgs);
        var t = isAct(s.ttsText) ? s.ttsText(c) : s.ttsText;
        return (t === undefined || t === null) ? undefined : t;
      };
    }

    return trigs;
  }

  function defineDuty(cfgs) {
    if (!cfgs || !Array.isArray(cfgs.mechanics)) {
      if (typeof consol !== 'undefined') consol.warn('defineFight: missing mechanics array');
      return;
    }

    var cues = [];
    for (var i = 0; i < cfgs.mechanics.length; i++) {
      var bit = cfgs.mechanics[i];
      if (!bit) continue;
      try {
        if (bit.__raw) {
          var rtBit = bit.__raw;
          if (!rtBit.id) rtBit.id = (cfgs.id || cfgs.name) + ' raw [' + i + ']';
          cues.push(rtBit);
        } else if (bit.__spec) {
          cues.push(buildCue(bit, cfgs, i));
        }
      }
      catch (e) { if (typeof consol !== 'undefined') consol.warn('fightkit build error in ' + (cfgs.name || cfgs.id), e); }
    }

    var phaseInit;
    if (isAct(cfgs.state)) phaseInit = cfgs.state;
    else if (cfgs.state) {
      var snapshots = JSON.stringify(cfgs.state);
      phaseInit = function () { return JSON.parse(snapshots); };
    } else {
      phaseInit = function () { return {}; };
    }

    triggerSets.push({
      id: cfgs.id || cfgs.name,
      name: cfgs.name,
      category: cfgs.category || 'Savage',
      boss: cfgs.boss || '',
      zoneId: cfgs.zoneId || 0,
      initData: phaseInit,
      triggers: cues,
      config: cfgs.config || [],
    });
  }

  globals.defineDuty    = defineDuty;
  globals.whenChant         = whenChant;
  globals.whenChantExtra    = whenChantExtra;
  globals.whenSkill      = whenSkill;
  globals.whenSign   = whenSign;
  globals.whenAura       = whenAura;
  globals.whenAuraLoss   = whenAuraLoss;
  globals.whenLeash       = whenLeash;
  globals.whenPush          = whenPush;
  globals.whenBodyControl = whenBodyControl;
  globals.whenBody    = whenBody;
  globals.whenTwo             = whenTwo;
  globals.raws            = raws;

})(globalThis);
