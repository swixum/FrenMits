// Dancing Mad: the Kefka-north answer, offered on their page.
//
// Their file offers two ways to call a black hole tether, true north and a clock
// number. This adds the third the group actually runs: north is wherever Kefka is,
// and every direction is read from there.
//
// The option and nothing else. This used to wrap their own tether calls and turn the
// directions under them; their black hole calls are dropped in dancingmad_one_call.js
// now and ours answer the mechanic, so all this row does is tell ours which words to
// use. It stays on their page because that is the page with the rows on it, and one
// row per question is the whole point of moving it there.
//
// Written as a patch beside their fights rather than as an edit inside one, so the
// whole folder of theirs can be replaced with a newer copy without losing it.
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

  // Offered wherever their own choices are offered.
  for (var c = 0; c < (duty.config || []).length; c++) {
    var choic = duty.config[c];
    if (choic.id !== STYLEBit || !choic.options) continue;

    var alreadies = false;
    for (var o = 0; o < choic.options.length; o++)
      if (choic.options[o].value === MODEBit) alreadies = true;

    if (!alreadies) choic.options.push({ value: MODEBit, label: LABELBit });
  }
})();
