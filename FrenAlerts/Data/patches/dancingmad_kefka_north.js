// Dancing Mad: black hole tethers called relative to Kefka.
//
// Ours, not theirs. Their file offers two ways to call a black hole tether, true
// north and a clock number, and this adds the third the group actually runs: north
// is wherever Kefka is, and every direction is read from there.
//
// Written as a patch beside their fights rather than as an edit inside one, so the
// whole folder of theirs can be replaced with a newer copy without losing it.
//
// How it works: their own code already knows where Kefka is and already knows how to
// say a direction. So when this mode is on, the black hole directions are rotated so
// Kefka's side reads as north, Kefka is told he is at north, and their trigger is
// then run in true-north mode. Their words, their logic, one rotation in front.
(function () {
  var STYLEBit = 'blackHoleTether';
  var MODEBit = 'kefka';
  var LABELBit = 'Kefka North Relative';

  function put() {
    for (var i = 0; i < triggerSets.length; i++)
      if (triggerSets[i] && triggerSets[i].id === 'DancingMadUltimate') return triggerSets[i];
    return null;
  }

  var duty = put();
  if (!duty) return;

  // Offer it wherever their own choices are offered.
  for (var c = 0; c < (duty.config || []).length; c++) {
    var choic = duty.config[c];
    if (choic.id !== STYLEBit || !choic.options) continue;

    var alreadies = false;
    for (var o = 0; o < choic.options.length; o++)
      if (choic.options[o].value === MODEBit) alreadies = true;

    if (!alreadies) choic.options.push({ value: MODEBit, label: LABELBit });
  }

  // Kefka's side as a cardinal, the same arithmetic their own tether ordering uses.
  function kefkaCardinals(pull) {
    var facingTwo = pull.kefkaTeleportDirNum;
    if (facingTwo === undefined || facingTwo === null) return -1;
    return Math.round(facingTwo / 2) % 4;
  }

  function turned(n, open) {
    return (n === undefined || n === null) ? n : ((n - open) % 4 + 4) % 4;
  }

  // Their map from a black hole to the cardinal it sits on.
  function rotateds(facingNums, open) {
    var awayTwo = {};
    for (var tagTwo in facingNums) awayTwo[tagTwo] = turned(facingNums[tagTwo], open);
    return awayTwo;
  }

  // Their list of tether cardinals, in the order they were collected, kept a list.
  //
  // The fight carries the directions twice and the two are not interchangeable:
  // blackHoleIdDirNums maps a black hole to its cardinal, blackHoleTetherDirNums is a
  // plain array of the cardinals collected this set. Two of the ten tether calls read
  // the map and the other eight read the array, so turning the map alone left eight
  // calls reading true north while the mode claimed to be relative, and worse: their
  // own ordering sorts the array by clockwise distance from Kefka, and that sort is
  // what hands one tether to the dps and the other to the support. Told Kefka was north
  // while the array still held true north, it sorted from the wrong corner and swapped
  // the two. With Kefka east and tethers north and south, the dps was sent to the north
  // tether when their own true-north call sends them to the south one.
  //
  // A list, not an object: their code slices it and reads its length, and the map
  // version above would come back as neither.
  function rotatedList(facingNums, open) {
    var awayTwo = [];
    for (var i = 0; i < facingNums.length; i++) awayTwo.push(turned(facingNums[i], open));
    return awayTwo;
  }

  // Every response of theirs is wrapped rather than a chosen few: a compiled
  // function does not reliably hand back its own source to read, and a wrapper that
  // does nothing unless the mode is on costs a comparison per call.
  for (var t = 0; t < duty.triggers.length; t++) {
    var cue = duty.triggers[t];
    if (typeof cue.response !== 'function') continue;

    cue.response = (function (originals) {
      return function (pull, hit, voice) {
        if (!pull.triggerSetConfig || pull.triggerSetConfig[STYLEBit] !== MODEBit)
          return originals(pull, hit, voice);

        var open = kefkaCardinals(pull);
        // Nobody knows where Kefka is, so there is nothing to be relative to and
        // their true-north answer is the honest one.
        if (open < 0) {
          pull.triggerSetConfig[STYLEBit] = 'true';
          try { return originals(pull, hit, voice); }
          finally { pull.triggerSetConfig[STYLEBit] = MODEBit; }
        }

        var heldFacings = pull.blackHoleIdDirNums;
        var heldList = pull.blackHoleTetherDirNums;
        var heldKefk = pull.kefkaTeleportDirNum;

        // Both direction stores turned by the same quarter, and Kefka then told he is at
        // north. Everything their code reads is now in one frame: sorting the turned list
        // clockwise from north is the same order as sorting the true one clockwise from
        // Kefka, and the words come out turned. Turn one and not the other and the two
        // halves disagree, which is the bug this line pair fixes.
        pull.blackHoleIdDirNums = rotateds(heldFacings || {}, open);
        pull.blackHoleTetherDirNums = rotatedList(heldList || [], open);
        pull.kefkaTeleportDirNum = 0;
        pull.triggerSetConfig[STYLEBit] = 'true';

        // Put back whatever was there, including the arrays: their collectors keep
        // pushing true-north cardinals into these between calls, and a turned value left
        // behind would be turned again on the next one.
        try { return originals(pull, hit, voice); }
        finally {
          pull.blackHoleIdDirNums = heldFacings;
          pull.blackHoleTetherDirNums = heldList;
          pull.kefkaTeleportDirNum = heldKefk;
          pull.triggerSetConfig[STYLEBit] = MODEBit;
        }
      };
    })(cue.response);
  }
})();
