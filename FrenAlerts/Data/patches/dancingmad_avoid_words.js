// Dancing Mad: the tell calls in the words the group actually says.
//
// Theirs read "Avoid Tell" and "Avoid Tells", which names the thing on the boss rather
// than what to do about it. swix calls it "Avoid everything", so that is what it says.
//
// Written as a patch beside their fights rather than as an edit inside one, so the whole
// folder of theirs can be replaced with a newer copy without losing it. The trigger sets
// hold a reference to the words object, so rewriting the entry reaches every trigger
// using it without any of them being touched.
(function () {
  // Theirs, ours. Matched on the whole string so a line that merely contains the words
  // is left alone.
  var words = {
    'Avoid Tell': 'Avoid everything',
    'Avoid Tells': 'Avoid everything',
  };

  function reword(strings) {
    if (!strings) return 0;

    var done = 0;
    for (var key in strings) {
      var one = strings[key];
      if (!one || typeof one !== 'object') continue;
      if (typeof one.en !== 'string') continue;
      if (!Object.prototype.hasOwnProperty.call(words, one.en)) continue;

      one.en = words[one.en];
      done++;
    }
    return done;
  }

  function duty() {
    for (var i = 0; i < triggerSets.length; i++)
      if (triggerSets[i] && triggerSets[i].id === 'DancingMadUltimate') return triggerSets[i];
    return null;
  }

  var fight = duty();
  if (!fight) return;

  // The shared words objects first, which is where these three actually live. Reached
  // through the triggers rather than by name, so a file that renames the object still
  // gets patched.
  var changed = 0;
  var seen = [];
  var triggers = fight.triggers || [];

  for (var t = 0; t < triggers.length; t++) {
    var strings = triggers[t] && triggers[t].outputStrings;
    if (!strings) continue;

    // The same object is hung off several triggers, so it is only worth doing once.
    var already = false;
    for (var s = 0; s < seen.length; s++) if (seen[s] === strings) already = true;
    if (already) continue;

    seen.push(strings);
    changed += reword(strings);
  }

  if (changed === 0)
    consol.log('dancingmad_avoid_words: nothing matched, their wording has changed');
})();
