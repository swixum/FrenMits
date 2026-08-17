// Dancing Mad: three of their strategy options in the words the group says out loud.
//
// Only the words on the dropdown move. The value behind each one is theirs and is what
// every trigger in the fight compares against, so no call changes.
//
// Written as a patch beside their fights rather than as an edit inside one, so the
// whole folder of theirs can be replaced with a newer copy without losing it.
(function () {
  // Their choice, their option value, our words for it.
  var words = [
    ['teleportent', 'clockwise', 'Bigbox'],
    ['forsaken', 'kroxy-rinon', 'Kroxy-Rinon 3-4-1'],
    ['blackHole', 'dsa', 'D>S>A Roles'],
  ];

  function duty() {
    for (var i = 0; i < triggerSets.length; i++)
      if (triggerSets[i] && triggerSets[i].id === 'DancingMadUltimate') return triggerSets[i];
    return null;
  }

  var fight = duty();
  if (!fight) return;

  for (var w = 0; w < words.length; w++) {
    for (var c = 0; c < (fight.config || []).length; c++) {
      var choice = fight.config[c];
      if (choice.id !== words[w][0] || !choice.options) continue;

      for (var o = 0; o < choice.options.length; o++)
        if (choice.options[o].value === words[w][1]) choice.options[o].label = words[w][2];
    }
  }
})();
