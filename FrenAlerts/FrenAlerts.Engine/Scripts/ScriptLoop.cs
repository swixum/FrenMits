namespace FrenAlerts.Engine.Scripts;

// The trigger loop, written in JavaScript and standing next to their files.
//
// Every condition, every collector and every text builder in their fights is a JS
// function expecting a JS data object and a JS matches object. Walking their triggers
// from C# means marshalling each of those on every hop and getting the difference
// between a missing field, a null and an undefined exactly right every time. Standing
// the loop beside them instead means their functions are called by JavaScript, the
// way they were written to be.
//
// Split in two on purpose. `__match` runs the moment an event arrives: it matches,
// asks the condition, honours the suppress window and runs the collector, because a
// collector reads the moment the line landed. `__say` runs when the call is due,
// after any delay, because several of their triggers collect for a few seconds and
// then say what they collected. Building the words in `__match` would read the state
// of the wrong moment, which is the one bug in this whole port that would never show
// up as an error.
public static class ScriptLoop
{
    public const string Driver = """
// ---- host: the loop their files were written against ----

var __zones = [];
var __data = {};
var __suppress = {};
var __pending = {};
var __seq = 0;

// A netRegex field holds a string, a list of strings, or nothing. Nothing means the
// trigger does not care. `capture` is an instruction to their own parser about what
// to hand back, never something to match on.
function __fieldMatches(want, got) {
  if (want === undefined || want === null) return true;
  if (got === undefined || got === null) return false;
  var g = String(got).toUpperCase();
  if (Object.prototype.toString.call(want) === '[object Array]') {
    for (var i = 0; i < want.length; i++)
      if (String(want[i]).toUpperCase() === g) return true;
    return false;
  }
  return String(want).toUpperCase() === g;
}

function __matches(rx, m) {
  if (!rx) return true;
  for (var k in rx) {
    if (k === 'capture') continue;
    if (!__fieldMatches(rx[k], m[k])) return false;
  }
  return true;
}

function __triggersHere() {
  var all = [];
  for (var z = 0; z < __zones.length; z++) {
    var set = triggerSets[__zones[z]];
    if (set && set.triggers) all = all.concat(set.triggers);
  }
  return all;
}

// An event, against every trigger of every fight loaded for this zone.
//
// Returns one row per trigger that wants to speak, carrying the seconds it should
// wait and a handle to the match it fired on. Nothing is worded here.
function __match(type, m, now) {
  var out = [];
  var triggers = __triggersHere();

  for (var i = 0; i < triggers.length; i++) {
    var t = triggers[i];
    if (t.type !== type) continue;
    if (!__matches(t.netRegex, m)) continue;

    try {
      if (t.condition && !t.condition(__data, m)) continue;

      // Their own once-per-window guard, keyed the way they key it.
      var suppress = (typeof t.suppressSeconds === 'function')
        ? t.suppressSeconds(__data, m) : t.suppressSeconds;
      if (suppress) {
        var until = __suppress[t.id];
        if (until !== undefined && now < until) continue;
        __suppress[t.id] = now + suppress;
      }

      // Immediately, because a collector reads the moment the line arrived.
      if (t.preRun) t.preRun(__data, m, makeOutput(t.outputStrings, 'en', t.id));

      var delay = (typeof t.delaySeconds === 'function')
        ? t.delaySeconds(__data, m) : t.delaySeconds;

      var handle = 'h' + (++__seq);
      __pending[handle] = { i: i, m: m };

      out.push({ id: t.id, handle: handle, delay: delay || 0 });
    }
    catch (err) { /* one trigger's bad moment is not the fight's */ }
  }
  return out;
}

// Their words, built when the call is due.
//
// Loudest first, and a response builder answers all three at once. `run` happens
// after the words, because several of theirs clear the very state the line was about
// to read.
function __say(handle) {
  var held = __pending[handle];
  delete __pending[handle];
  if (!held) return null;

  var triggers = __triggersHere();
  var t = triggers[held.i];
  if (!t) return null;

  var m = held.m;
  var out = makeOutput(t.outputStrings, 'en', t.id);

  try {
    if (t.promise) t.promise(__data, m, out);

    var src = t;
    if (t.response) {
      var r = (typeof t.response === 'function') ? t.response(__data, m, out) : t.response;
      if (r) src = r;
    }

    var fields = ['alarmText', 'alertText', 'infoText'];
    var levels = { alarmText: 2, alertText: 1, infoText: 0 };
    var text = null, level = 0;

    for (var i = 0; i < fields.length; i++) {
      var f = src[fields[i]];
      if (f === undefined || f === null) continue;
      var line = (typeof f === 'function') ? f(__data, m, out) : f;
      if (line === undefined || line === null || line === '') continue;
      if (line && line.en) line = line.en;
      text = String(line); level = levels[fields[i]];
      break;
    }

    var spoken = null;
    if (t.tts !== undefined && t.tts !== null) {
      var s = (typeof t.tts === 'function') ? t.tts(__data, m, out) : t.tts;
      if (s && s.en) s = s.en;
      if (s !== undefined && s !== null && s !== '') spoken = String(s);
    }

    if (t.run) t.run(__data, m, out);

    var hold = (typeof t.durationSeconds === 'function')
      ? t.durationSeconds(__data, m) : t.durationSeconds;

    if (text === null && spoken === null) return null;
    return { id: t.id, text: text || '', speech: spoken === null ? (text || '') : spoken,
             level: level, hold: hold || 0 };
  }
  catch (err) { return null; }
}

// A pull ending drops whatever was waiting: a call from the pull before is worse
// than no call at all.
function __forget() { __pending = {}; __suppress = {}; }
""";
}
