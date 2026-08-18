// Dancing Mad: the triggers of theirs we answer ourselves, taken out so a mechanic is
// called once.
//
// Nothing is taken out, and that is the point of the file rather than a gap in it.
//
// It was written believing both sets are loaded and both fire. They are not. A zone
// their fight covers builds no module of ours at all: FightLoader.Build returns an
// empty engine before it reaches AddFightModule, deliberately, so that half of ours
// beside all of theirs cannot make two calls for one mechanic. Ours are still listed
// on the settings page and still counted there, which is what made the belief so easy
// to hold and so hard to see through.
//
// So a trigger of theirs dropped here is a mechanic that goes silent. Seven of P5's
// did, for as long as this list had entries in it, and eighteen more in P2 and P3 for
// the few hours the list named them: swix opened Dancing Mad and nothing called.
//
// The list stays empty until our own module runs in a covered zone. On the day it
// does, the ids below are the ones to put back, and the entry has to name what ours
// says instead, not just that ours exists.
//
// A patch rather than an edit inside their file, so replacing their whole folder with a
// newer copy cannot undo it.
(function () {
  // Their trigger id, and why ours is the one kept.
  var drop = {};

  function duty() {
    for (var i = 0; i < triggerSets.length; i++)
      if (triggerSets[i] && triggerSets[i].id === 'DancingMadUltimate') return triggerSets[i];
    return null;
  }

  var fight = duty();
  if (!fight || !fight.triggers) return;

  var kept = [];
  var taken = 0;

  for (var t = 0; t < fight.triggers.length; t++) {
    var one = fight.triggers[t];
    if (one && one.id && Object.prototype.hasOwnProperty.call(drop, one.id)) {
      taken++;
      continue;
    }
    kept.push(one);
  }

  fight.triggers = kept;

  // Said out loud, because a patch that matches nothing is exactly as quiet as one that
  // never loaded, and that has already cost a night once.
  var wanted = 0;
  for (var k in drop) wanted++;
  if (taken !== wanted)
    console.log('dancingmad_one_call: took out ' + taken + ' of ' + wanted
                + ', their trigger ids have changed');
})();
