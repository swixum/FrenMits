// Dancing Mad: the triggers of theirs we answer ourselves, taken out so a mechanic is
// called once.
//
// Both sets are loaded and both fire. Where the two answer the same cast, the mechanic
// is said twice, which is how phase 4's raidwides were heard. Those were fixed the other
// way round, by dropping ours and keeping theirs, because theirs said more. These are
// the ones where ours says more, so theirs is the one that goes.
//
// A patch rather than an edit inside their file, so replacing their whole folder with a
// newer copy cannot undo it.
(function () {
  // Their trigger id, and why ours is the one kept.
  var drop = {
    // Theirs says a bare "Spread". Ours says "Spread Positions", which is the call
    // swix's group makes.
    'DMU P5 Maddening Orchestra': 'ours says the positions',

    // Theirs says "Raidwide x4", which names the mechanic. Ours says "Go to Role
    // Spots", which is what to do about it.
    'DMU P5 Ultima Repeater': 'ours says where to stand',

    // Forsaken. Every part of it was said twice: theirs answers the same cast with
    // a big-aoe alert and the same four sets with "move away", so a Forsaken made
    // ten calls where five would do.
    //
    // Ours says "Gather South for raidwide" on the cast, which is where to be
    // rather than what is coming.
    'DMU P5 Forsaken': 'ours says where to gather',

    // And "move" on each set, which is the same word theirs uses.
    'DMU P5 Forsaken Move': 'ours says the same thing',

    // The stack is not called. swix asked for the move only on Forsaken, and said so
    // for phase 2's first: naming who to stack on is the line he had taken out there.
    'DMU P5 Forsaken Stack': 'only the move is wanted',

    // Chaotic Flood. Theirs says the cast is a raidwide; ours reads the lines and
    // says where to stand and which way it turns, on the flood's own countdown.
    'DMU P5 Flood': 'ours says where to stand',

    // And theirs says "move away" on the same hits ours already says "move" on.
    'DMU P5 Flood Move': 'ours says the same thing',
  };

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
