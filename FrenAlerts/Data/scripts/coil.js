const restartTrio = (pull, tri) => {
 pull.trio = tri;
 pull.shakers = [];
 pull.megaStack = [];
 pull.combatantData = {};
};

const placeToTurnAmount = (place) => {
 return xyWordToTurnAmount(place.x, place.y);
};

const xyWordToTurnAmount = (x, y) => {
 return xyToTurnAmount(parseFloat(x), parseFloat(y));
};

const xyToTurnAmount = (x, y) => {
 return (Math.round(180 - 180 * Math.atan2(x, y) / Math.PI) % 360);
};

const centerXBit = 0;
const centerYBit = 0;

var isClockwis = (open, compar) => {
 let isCWBit = false;
 if (compar > open)
 isCWBit = compar - open <= 180;
 else if (compar < open)
 isCWBit = open - compar >= 180;
 return isCWBit;
};

var modDistanc = (marksTwo, dragonsTwo) => {
 const oneWaies = (dragonsTwo - marksTwo + 8) % 8;
 const otherWaies = (marksTwo - dragonsTwo + 8) % 8;
 const distanc = Math.min(oneWaies, otherWaies);

 return distanc;
};

var badSpot = (marksTwo, dragonsTwo) => {
 const bads = [];
 const distanc = modDistanc(marksTwo, dragonsTwo);

 if ((marksTwo + distanc + 8) % 8 === dragonsTwo) {
 for (let i = 0; i <= distanc; ++i)
 bads.push((marksTwo + i) % 8);
 if (distanc === 1)
 bads.push((marksTwo - 1 + 8) % 8);
 } else {
 for (let i = 0; i <= distanc; ++i)
 bads.push((marksTwo - i + 8) % 8);
 if (distanc === 1)
 bads.push((marksTwo + 1) % 8);
 }
 return bads;
};

var findDragonMark = function (arraies) {
 const markTwo = [-1, -1, -1];
 let isWideThirdDiv = false;

 const dragonTwo = [];
 for (let i = 0; i < 8; ++i) {
 if (arraies[i])
 dragonTwo.push(i);
 }

 if (dragonTwo.length !== 5)
 return;

 const [d0Bit, d1Bit, d2Bit, d3Bit, d4Bit] = dragonTwo;
 if (
 d0Bit === undefined || d1Bit === undefined || d2Bit === undefined ||
 d3Bit === undefined || d4Bit === undefined
 )
 return;

 if (d0Bit + 1 === d1Bit) {
 markTwo[0] = (d0Bit - 1 + 8) % 8;
 } else {
 markTwo[0] = Math.floor((d0Bit + d1Bit) / 2);
 }

 if (d1Bit === d2Bit - 1) {
 markTwo[1] = d2Bit + 1;
 } else {
 markTwo[1] = d2Bit - 1;
 }

 if (d3Bit + 1 === d4Bit) {
 markTwo[2] = (d4Bit + 1) % 8;

 const distanc = markTwo[1] === d2Bit - 1 ? 2 : 4;
 if (d3Bit >= d2Bit + distanc)
 markTwo[2] = d3Bit - 1;
 } else {
 markTwo[2] = Math.ceil((d3Bit + d4Bit) / 2);
 if (markTwo[1] === d3Bit && markTwo[2] === markTwo[1] + 1) {
 markTwo[2] = (d4Bit + 1) % 8;
 isWideThirdDiv = true;
 }
 }

 const bads = badSpot(markTwo[0], d0Bit);
 bads.concat(badSpot(markTwo[0], d1Bit));

 return {
 wideThirdDive: isWideThirdDiv,
 unsafeThirdMark: bads.includes(markTwo[2]),
 marks: markTwo,
 };
};

var ucobTimelineCues = [
{
  id: 'UCU Bahamut\'s Claw',
  type: 'StartsUsing',
  netRegex: { id: '26B5', source: 'Nael deus Darnus' },
  response: Response.tankBuster(),
},
{
  id: 'UCU Plummet',
  type: 'StartsUsing',
  netRegex: { id: '26A8', source: 'Twintania' },
  response: Response.tankCleave(),
},
{
  id: 'UCU Flare Breath',
  type: 'StartsUsing',
  netRegex: { id: '26D4', source: 'Bahamut Prime' },
  response: Response.tankCleave(),
},
];

var ucobCues = [
 {
 id: 'UCU Firescorched Gain',
 type: 'GainsEffect',
 netRegex: { effectId: '1D0' },
 condition: Condition.targetIsYou(),
 run: (pull) => pull.fireDebuff = true,
 },
 {
 id: 'UCU Firescorched Lose',
 type: 'LosesEffect',
 netRegex: { effectId: '1D0' },
 condition: Condition.targetIsYou(),
 run: (pull) => pull.fireDebuff = false,
 },
 {
 id: 'UCU Icebitten Gain',
 type: 'GainsEffect',
 netRegex: { effectId: '1D1' },
 condition: Condition.targetIsYou(),
 run: (pull) => pull.iceDebuff = true,
 },
 {
 id: 'UCU Icebitten Lose',
 type: 'LosesEffect',
 netRegex: { effectId: '1D1' },
 condition: Condition.targetIsYou(),
 run: (pull) => pull.iceDebuff = false,
 },
 {
 id: 'UCU Fireball Counter',
 type: 'Ability',
 netRegex: { id: '26C5', source: 'Firehorn' },
 run: (pull, hit) => {
 (pull.fireballs[pull.naelFireballCount] ??= []).push(hit.target);
 },
 },
 {
 id: 'UCU Quickmarch Phase',
 type: 'StartsUsing',
 netRegex: { id: '26E2', source: 'Bahamut Prime', capture: false },
 delaySeconds: 1,
 run: (pull) => restartTrio(pull, 'quickmarch'),
 },
 {
 id: 'UCU Blackfire Phase',
 type: 'StartsUsing',
 netRegex: { id: '26E3', source: 'Bahamut Prime', capture: false },
 delaySeconds: 1,
 run: (pull) => restartTrio(pull, 'blackfire'),
 },
 {
 id: 'UCU Fellruin Phase',
 type: 'StartsUsing',
 netRegex: { id: '26E4', source: 'Bahamut Prime', capture: false },
 delaySeconds: 1,
 run: (pull) => restartTrio(pull, 'fellruin'),
 },
 {
 id: 'UCU Heavensfall Phase',
 type: 'StartsUsing',
 netRegex: { id: '26E5', source: 'Bahamut Prime', capture: false },
 delaySeconds: 1,
 run: (pull) => restartTrio(pull, 'heavensfall'),
 },
 {
 id: 'UCU Tenstrike Phase',
 type: 'StartsUsing',
 netRegex: { id: '26E6', source: 'Bahamut Prime', capture: false },
 delaySeconds: 1,
 run: (pull) => restartTrio(pull, 'tenstrike'),
 },
 {
 id: 'UCU Octet Phase',
 type: 'StartsUsing',
 netRegex: { id: '26E7', source: 'Bahamut Prime', capture: false },
 delaySeconds: 1,
 run: (pull) => restartTrio(pull, 'octet'),
 },

 {
 id: 'UCU Twisters',
 type: 'StartsUsing',
 netRegex: { id: '26AA', source: 'Twintania', capture: false },
 alertText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Twisters',
 de: 'WirbelstÃ¼rme',
 fr: 'Tornades',
 ja: 'å¤§ç«œå·»',
 cn: 'æ—‹é£Ž',
 ko: 'íšŒì˜¤ë¦¬',
 tc: 'æ—‹é¢¨',
 },
 },
 },
 {
 id: 'UCU Death Sentence',
 type: 'StartsUsing',
 netRegex: { id: '26A9', source: 'Twintania' },
 response: Response.tankBusterSwap(),
 },
 {
 id: 'UCU Hatch Collect',
 type: 'HeadMarker',
 netRegex: { id: '0076' },
 run: (pull, hit) => {
 pull.hatch ??= [];
 pull.hatch.push(hit.target);
 },
 },
 {
 id: 'UCU Hatch Marker YOU',
 type: 'HeadMarker',
 netRegex: { id: '0076' },
 condition: Condition.targetIsYou(),
 alarmText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Hatch on YOU',
 de: 'Austritt auf DIR',
 fr: 'Ã‰closion sur VOUS',
 ja: 'è‡ªåˆ†ã«é­”åŠ›çˆ†æ•£',
 cn: 'é»‘çƒç‚¹å',
 ko: 'ë‚˜ì—ê²Œ ë§ˆë ¥ì—°ì„±',
 tc: 'é»‘çƒé»žå',
 },
 },
 },
 {
 id: 'UCU Hatch Callouts',
 type: 'HeadMarker',
 netRegex: { id: '0076', capture: false },
 delaySeconds: 0.25,
 infoText: (pull, _hit, voice) => {
 if (!pull.hatch)
 return;
 const hatche = pull.hatch.map((n) => pull.party.member(n));
 delete pull.hatch;
 return voice.text({ players: hatche });
 },
 outputStrings: {
 text: {
 en: 'Hatch: ${players}',
 de: 'Austritt: ${players}',
 fr: 'Ã‰closion : ${players}',
 ja: 'é­”åŠ›çˆ†æ•£${players}',
 cn: 'é»‘çƒç‚¹ï¼š${players}',
 ko: 'ë§ˆë ¥ì—°ì„±: ${players}',
 tc: 'é»‘çƒé»ž: ${players}',
 },
 },
 },
 {
 id: 'UCU Hatch Cleanup',
 type: 'HeadMarker',
 netRegex: { id: '0076', capture: false },
 delaySeconds: 5,
 run: (pull) => delete pull.hatch,
 },
 {
 id: 'UCU Twintania Phase Change Watcher',
 type: 'CombatantMemory',
 netRegex: { id: '40[0-9A-F]{6}', pair: [{ key: 'BNpcID', value: '1E88FF' }], capture: false },
 condition: (pull) => pull.currentPhase < 4,
 sound: 'Long',
 infoText: (pull, _hit, voice) => voice.text({ num: pull.currentPhase }),
 run: (pull) => {
 pull.currentPhase++;
 },
 outputStrings: {
 text: {
 en: 'Phase ${num} Push',
 de: 'Phase ${num} StoÃŸ',
 fr: 'Phase ${num} poussÃ©e',
 ja: 'ãƒ•ã‚§ãƒ¼ã‚º${num}',
 cn: 'P${num}å‡†å¤‡',
 ko: 'íŠ¸ìœˆ íŽ˜ì´ì¦ˆ${num}',
 tc: 'P${num}æº–å‚™',
 },
 },
 },

 {
 id: 'UCU Nael Quote 1',
 type: 'NpcYell',
 netRegex: { npcYellId: '1961', capture: false },
 durationSeconds: 6,
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Spread => In',
 de: 'Verteilen => Rein',
 fr: 'Dispersez-vous => IntÃ©rieur',
 ja: 'æ•£é–‹ => å¯†ç€',
 cn: 'åˆ†æ•£ => é è¿‘',
 ko: 'ì‚°ê°œ => ì•ˆìœ¼ë¡œ',
 tc: 'åˆ†æ•£ => é è¿‘',
 },
 },
 },
 {
 id: 'UCU Nael Quote 2',
 type: 'NpcYell',
 netRegex: { npcYellId: '1960', capture: false },
 durationSeconds: 6,
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Spread => Out',
 de: 'Verteilen => Raus',
 fr: 'Dispersez-vous => ExtÃ©rieur',
 ja: 'æ•£é–‹ => é›¢ã‚Œ',
 cn: 'åˆ†æ•£ => è¿œç¦»',
 ko: 'ì‚°ê°œ => ë°–ìœ¼ë¡œ',
 tc: 'åˆ†æ•£ => é é›¢',
 },
 },
 },
 {
 id: 'UCU Nael Quote 3',
 type: 'NpcYell',
 netRegex: { npcYellId: '195F', capture: false },
 durationSeconds: 6,
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Stack => In',
 de: 'Sammeln => Rein',
 fr: 'Packez-vous => IntÃ©rieur',
 ja: 'é ­å‰²ã‚Š => å¯†ç€',
 cn: 'åˆ†æ‘Š => é è¿‘',
 ko: 'ì‰ì–´ => ì•ˆìœ¼ë¡œ',
 tc: 'åˆ†æ”¤ => é è¿‘',
 },
 },
 },
 {
 id: 'UCU Nael Quote 4',
 type: 'NpcYell',
 netRegex: { npcYellId: '195E', capture: false },
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Stack => Out',
 de: 'Sammeln => Raus',
 fr: 'Packez-vous => ExtÃ©rieur',
 ja: 'é ­å‰²ã‚Š => é›¢ã‚Œ',
 cn: 'åˆ†æ‘Š => è¿œç¦»',
 ko: 'ì‰ì–´ => ë°–ìœ¼ë¡œ',
 tc: 'åˆ†æ”¤ => é é›¢',
 },
 },
 },
 {
 id: 'UCU Nael Quote 5',
 type: 'NpcYell',
 netRegex: { npcYellId: '195D', capture: false },
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'In => Stack',
 de: 'Rein => Sammeln',
 fr: 'IntÃ©rieur => Packez-vous',
 ja: 'å¯†ç€ => é ­å‰²ã‚Š',
 cn: 'é è¿‘ => åˆ†æ‘Š',
 ko: 'ì•ˆìœ¼ë¡œ => ì‰ì–´',
 tc: 'é è¿‘ => åˆ†æ”¤',
 },
 },
 },
 {
 id: 'UCU Nael Quote 6',
 type: 'NpcYell',
 netRegex: { npcYellId: '195C', capture: false },
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'In => Out',
 de: 'Rein => Raus',
 fr: 'IntÃ©rieur => ExtÃ©rieur',
 ja: 'å¯†ç€ => é›¢ã‚Œ',
 cn: 'é è¿‘ => è¿œç¦»',
 ko: 'ì•ˆìœ¼ë¡œ => ë°–ìœ¼ë¡œ',
 tc: 'é è¿‘ => é é›¢',
 },
 },
 },
 {
 id: 'UCU Nael Quote 7',
 type: 'NpcYell',
 netRegex: { npcYellId: '1965', capture: false },
 delaySeconds: 4,
 durationSeconds: 6,
 alertText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Away from Tank => Stack',
 de: 'Weg vom Tank => Sammeln',
 fr: 'Ã‰loignez-vous du tank => Packez-vous',
 ja: 'ã‚¿ãƒ³ã‚¯ã‹ã‚‰é›¢ã‚Œ => é ­å‰²ã‚Š',
 cn: 'è¿œç¦»å¦å…‹ => åˆ†æ‘Š',
 ko: 'íƒ±ì»¤ í”¼í•˜ê¸° => ì‰ì–´',
 tc: 'é é›¢å¦å…‹ => åˆ†æ”¤',
 },
 },
 },
 {
 id: 'UCU Nael Quote 8',
 type: 'NpcYell',
 netRegex: { npcYellId: '1964', capture: false },
 delaySeconds: 4,
 durationSeconds: 6,
 alertText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Spread => Away from Tank',
 de: 'Verteilen => Weg vom Tank',
 fr: 'Dispersez-vous => Ã‰loignez-vous du Tank',
 ja: 'æ•£é–‹ => ã‚¿ãƒ³ã‚¯ã‹ã‚‰é›¢ã‚Œ',
 cn: 'åˆ†æ•£ => è¿œç¦»å¦å…‹',
 ko: 'ì‚°ê°œ => íƒ±ì»¤ í”¼í•˜ê¸°',
 tc: 'åˆ†æ•£ => é é›¢å¦å…‹',
 },
 },
 },
 {
 id: 'UCU Nael Quote 9',
 type: 'NpcYell',
 netRegex: { npcYellId: '1966', capture: false },
 durationSeconds: 9,
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Spread => In',
 de: 'Verteilen => Rein',
 fr: 'Dispersez-vous => IntÃ©rieur',
 ja: 'æ•£é–‹ => å¯†ç€',
 cn: 'åˆ†æ•£ => é è¿‘',
 ko: 'ì‚°ê°œ => ì•ˆìœ¼ë¡œ',
 tc: 'åˆ†æ•£ => é è¿‘',
 },
 },
 },
 {
 id: 'UCU Nael Quote 10',
 type: 'NpcYell',
 netRegex: { npcYellId: '1967', capture: false },
 durationSeconds: 9,
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'In => Spread',
 de: 'Rein => Verteilen',
 fr: 'IntÃ©rieur => Dispersez-vous',
 ja: 'å¯†ç€ => æ•£é–‹',
 cn: 'é è¿‘ => åˆ†æ•£',
 ko: 'ì•ˆìœ¼ë¡œ => ì‚°ê°œ',
 tc: 'é è¿‘ => åˆ†æ•£',
 },
 },
 },
 {
 id: 'UCU Nael Quote 11',
 type: 'NpcYell',
 netRegex: { npcYellId: '196B', capture: false },
 durationSeconds: 9,
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'In => Out => Spread',
 de: 'Rein => Raus => Verteilen',
 fr: 'IntÃ©rieur => ExtÃ©rieur => Dispersion',
 ja: 'å¯†ç€ => é›¢ã‚Œ => æ•£é–‹',
 cn: 'é è¿‘ => è¿œç¦» => åˆ†æ•£',
 ko: 'ì•ˆìœ¼ë¡œ => ë°–ìœ¼ë¡œ => ì‚°ê°œ',
 tc: 'é è¿‘ => é é›¢ => åˆ†æ•£',
 },
 },
 },
 {
 id: 'UCU Nael Quote 12',
 type: 'NpcYell',
 netRegex: { npcYellId: '196A', capture: false },
 durationSeconds: 9,
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'In => Spread => Stack',
 de: 'Rein => Verteilen => Sammeln',
 fr: 'IntÃ©rieur => Dispersion => Package',
 ja: 'å¯†ç€ => æ•£é–‹ => é ­å‰²ã‚Š',
 cn: 'é è¿‘ => åˆ†æ•£ => åˆ†æ‘Š',
 ko: 'ì•ˆìœ¼ë¡œ => ì‚°ê°œ => ì‰ì–´',
 tc: 'é è¿‘ => åˆ†æ•£ => åˆ†æ”¤',
 },
 },
 },
 {
 id: 'UCU Nael Quote 13',
 type: 'NpcYell',
 netRegex: { npcYellId: '1968', capture: false },
 durationSeconds: 9,
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Out => Stack => Spread',
 de: 'Raus => Sammeln => Verteilen',
 fr: 'ExtÃ©rieur => Package => Dispersion',
 ja: 'é›¢ã‚Œ => é ­å‰²ã‚Š => æ•£é–‹',
 cn: 'è¿œç¦» => åˆ†æ‘Š => åˆ†æ•£',
 ko: 'ë°–ìœ¼ë¡œ => ì‰ì–´ => ì‚°ê°œ',
 tc: 'é é›¢ => åˆ†æ”¤ => åˆ†æ•£',
 },
 },
 },
 {
 id: 'UCU Nael Quote 14',
 type: 'NpcYell',
 netRegex: { npcYellId: '1969', capture: false },
 durationSeconds: 9,
 infoText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Out => Spread => Stack',
 de: 'Raus => Verteilen => Sammeln',
 fr: 'ExtÃ©rieur => Dispersion => Package',
 ja: 'é›¢ã‚Œ => æ•£é–‹ => é ­å‰²ã‚Š',
 cn: 'è¿œç¦» => åˆ†æ•£ => åˆ†æ‘Š',
 ko: 'ë°–ìœ¼ë¡œ => ì‚°ê°œ => ì‰ì–´',
 tc: 'é é›¢ => åˆ†æ•£ => åˆ†æ”¤',
 },
 },
 },
 {
 id: 'UCU Nael Thunder Collect',
 type: 'Ability',
 netRegex: { source: 'Thunderwing', id: '26C7' },
 run: (pull, hit) => {
 pull.thunderDebuffs.push(hit.target);
 if (pull.me === hit.target)
 pull.thunderOnYou = true;
 },
 },
 {
 id: 'UCU Nael Thunderstruck',
 type: 'Ability',
 netRegex: { source: 'Thunderwing', id: '26C7', capture: false },
 delaySeconds: 0.5,
 suppressSeconds: 5,
 alarmText: (pull, _hit, voice) => {
 if (pull.thunderOnYou)
 return voice.thunderOnYou();
 },
 infoText: (pull, _hit, voice) => {
 if (!pull.thunderOnYou) {
 const [thunder1s, thunder2s] = pull.thunderDebuffs.map((p) => pull.party.member(p));
 return voice.thunderOnOthers({ player1: thunder1s, player2: thunder2s });
 }
 },
 run: (pull) => {
 pull.thunderDebuffs = [];
 pull.thunderOnYou = false;
 },
 outputStrings: {
 thunderOnYou: {
 en: 'Thunder on YOU',
 de: 'Blitz auf DIR',
 fr: 'Foudre sur VOUS',
 ja: 'è‡ªåˆ†ã«ã‚µãƒ³ãƒ€ãƒ¼',
 cn: 'é›·ç‚¹å',
 ko: 'ë‚˜ì—ê²Œ ë²ˆê°œ',
 tc: 'é›·é»žå',
 },
 thunderOnOthers: {
 en: 'Thunder on ${player1}, ${player2}',
 de: 'Blitz auf ${player1}, ${player2}',
 fr: 'Foudre sur ${player1}, ${player2}',
 cn: 'é›·ç‚¹ ${player1}, ${player2}',
 ko: 'ë²ˆê°œ ${player1}, ${player2}',
 tc: 'é›·é»ž ${player1}, ${player2}',
 },
 },
 },
 {
 id: 'UCU Nael Your Doom',
 type: 'GainsEffect',
 netRegex: { effectId: 'D2' },
 condition: (pull, hit) => {
 return pull.me === hit.target;
 },
 durationSeconds: (_pull, hit) => {
 if (parseFloat(hit.duration) <= 6)
 return 3;

 if (parseFloat(hit.duration) <= 10)
 return 6;

 return 9;
 },
 suppressSeconds: 20,
 alarmText: (_pull, hit, voice) => {
 if (parseFloat(hit.duration) <= 6)
 return voice.doom1();
 if (parseFloat(hit.duration) <= 10)
 return voice.doom2();
 return voice.doom3();
 },
 tts: (_pull, hit, voice) => {
 if (parseFloat(hit.duration) <= 6)
 return voice.justNumber({ num: '1' });

 if (parseFloat(hit.duration) <= 10)
 return voice.justNumber({ num: '2' });

 return voice.justNumber({ num: '3' });
 },
 outputStrings: {
 doom1: {
 en: 'Doom #1 on YOU',
 de: 'VerhÃ¤ngnis #1 auf DIR',
 fr: 'Glas #1 sur VOUS',
 ja: 'è‡ªåˆ†ã«ä¸€ç•ªç›®æ­»ã®å®£å‘Š',
 cn: 'æ­»å®£ä¸€å·ç‚¹å',
 ko: 'ì£½ìŒì˜ ì„ ê³  1ë²ˆ',
 tc: 'æ­»å®£#1 é»žå',
 },
 doom2: {
 en: 'Doom #2 on YOU',
 de: 'VerhÃ¤ngnis #2 auf DIR',
 fr: 'Glas #2 sur VOUS',
 ja: 'è‡ªåˆ†ã«äºŒç•ªç›®æ­»ã®å®£å‘Š',
 cn: 'æ­»å®£äºŒå·ç‚¹å',
 ko: 'ì£½ìŒì˜ ì„ ê³  2ë²ˆ',
 tc: 'æ­»å®£#2 é»žå',
 },
 doom3: {
 en: 'Doom #3 on YOU',
 de: 'VerhÃ¤ngnis #3 auf DIR',
 fr: 'Glas #3 sur VOUS',
 ja: 'è‡ªåˆ†ã«ä¸‰ç•ªç›®æ­»ã®å®£å‘Š',
 cn: 'æ­»å®£ä¸‰å·ç‚¹å',
 ko: 'ì£½ìŒì˜ ì„ ê³  3ë²ˆ',
 tc: 'æ­»å®£#3 é»žå',
 },
 justNumber: {
 en: '${num}',
 de: '${num}',
 fr: '${num}',
 ja: '${num}',
 cn: '${num}',
 ko: '${num}',
 tc: '${num}',
 },
 },
 },
 {
 id: 'UCU Doom Init',
 type: 'GainsEffect',
 netRegex: { effectId: 'D2' },
 run: (pull, hit) => {
 pull.dooms ??= [null, null, null];
 let sequence = null;
 if (parseFloat(hit.duration) < 9)
 sequence = 0;
 else if (parseFloat(hit.duration) < 14)
 sequence = 1;
 else
 sequence = 2;

 if (sequence !== null && pull.dooms[sequence] === null)
 pull.dooms[sequence] = hit.target;
 },
 },
 {
 id: 'UCU Doom Cleanup',
 type: 'GainsEffect',
 netRegex: { effectId: 'D2', capture: false },
 delaySeconds: 20,
 run: (pull) => {
 delete pull.dooms;
 delete pull.doomCount;
 },
 },
 {
 id: 'UCU Nael Cleanse Callout',
 type: 'Ability',
 netRegex: { source: 'Fang Of Light', id: '26CA', capture: false },
 infoText: (pull, _hit, voice) => {
 pull.doomCount ??= 0;
 let labelTwo;
 if (pull.dooms)
 labelTwo = pull.dooms[pull.doomCount];
 pull.doomCount++;
 if (typeof labelTwo === 'string')
 return voice.text({ num: pull.doomCount, player: pull.party.member(labelTwo) });
 },
 outputStrings: {
 text: {
 en: 'Cleanse #${num}: ${player}',
 de: 'Reinige #${num}: ${player}',
 fr: 'Purifiez #${num}: ${player}',
 ja: 'è§£é™¤ã«ç•ªç›®${num}: ${player}',
 cn: 'è§£é™¤æ­»å®£ #${num}: ${player}',
 ko: 'ì„ ê³  í•´ì œ ${num}: ${player}',
 tc: 'è§£é™¤æ­»å®£ #${num}: ${player}',
 },
 },
 },
 {
 id: 'UCU Nael Fireball 1',
 type: 'Ability',
 netRegex: { source: 'Ragnarok', id: '26B8', capture: false },
 delaySeconds: 35,
 suppressSeconds: 99999,
 infoText: (_pull, _hit, voice) => voice.text(),
 run: (pull) => pull.naelFireballCount = 1,
 outputStrings: {
 text: {
 en: 'Fire IN',
 de: 'Feuer INNEN',
 fr: 'Feu Ã  l\'INTÃ‰RIEUR',
 ja: 'ãƒ•ã‚¡ã‚¤ã‚¢ãƒœãƒ¼ãƒ«ã¯å¯†ç€',
 cn: 'äººç¾¤ç«1',
 ko: 'ë¶ˆ ê°™ì´ë§žê¸°',
 tc: 'äººç¾¤ç«1',
 },
 },
 },
 {
 id: 'UCU Nael Fireball 2',
 type: 'Ability',
 netRegex: { source: 'Ragnarok', id: '26B8', capture: false },
 delaySeconds: 51,
 suppressSeconds: 99999,
 alertText: (pull, _hit, voice) => {
 if (!pull.fireballs[1]?.includes(pull.me))
 return voice.fireOutBeInIt();
 },
 infoText: (pull, _hit, voice) => {
 if (pull.fireballs[1]?.includes(pull.me))
 return voice.fireOut();
 },
 run: (pull) => pull.naelFireballCount = 2,
 outputStrings: {
 fireOut: {
 en: 'Fire OUT',
 de: 'Feuer AUÃŸEN',
 fr: 'Feu Ã  l\'EXTÃ‰RIEUR',
 ja: 'ãƒ•ã‚¡ã‚¤ã‚¢ãƒœãƒ¼ãƒ«ã¯é›¢ã‚Œ',
 cn: 'å•åƒç«2',
 ko: 'ë¶ˆ ëŒ€ìƒìž ë°–ìœ¼ë¡œ',
 tc: 'å–®åƒç«2',
 },
 fireOutBeInIt: {
 en: 'Fire OUT: Be in it',
 de: 'Feuer AUÃŸEN: Drin sein',
 fr: 'Feu Ã  l\'EXTÃ‰RIEUR : Allez dessus',
 ja: 'ãƒ•ã‚¡ã‚¤ã‚¢ãƒœãƒ¼ãƒ«ã¯é›¢ã‚Œ: è‡ªåˆ†ã«å¯†ç€',
 cn: 'åŽ»åƒç«2',
 ko: 'ë¶ˆ ëŒ€ìƒìž ë°–ìœ¼ë¡œ: ë‚˜ëŠ” ê°™ì´ ë§žê¸°',
 tc: 'åŽ»åƒç«2',
 },
 },
 },
 {
 id: 'UCU Nael Fireball 3',
 type: 'Ability',
 netRegex: { source: 'Ragnarok', id: '26B8', capture: false },
 delaySeconds: 77,
 suppressSeconds: 99999,
 alertText: (pull, _hit, voice) => {
 if (pull.fireballs[1]?.includes(pull.me) && pull.fireballs[2]?.includes(pull.me))
 return voice.fireInAvoid();
 },
 infoText: (pull, _hit, voice) => {
 const tookTw = pull.fireballs[1]?.filter((p) => {
 return pull.fireballs[2]?.includes(p);
 });
 if (tookTw?.includes(pull.me))
 return;

 if (tookTw && tookTw.length > 0) {
 const members = tookTw.map((labelTwo) => pull.party.member(labelTwo));
 return voice.fireInPlayersOut({ players: members });
 }
 return voice.fireIn();
 },
 run: (pull) => pull.naelFireballCount = 3,
 outputStrings: {
 fireIn: {
 en: 'Fire IN',
 de: 'Feuer INNEN',
 fr: 'Feu Ã  l\'INTÃ‰RIEUR',
 ja: 'ãƒ•ã‚¡ã‚¤ã‚¢ãƒœãƒ¼ãƒ«ã¯å¯†ç€',
 cn: 'äººç¾¤ç«3',
 ko: 'ë¶ˆ ê°™ì´ë§žê¸°',
 tc: 'äººç¾¤ç«3',
 },
 fireInPlayersOut: {
 en: 'Fire IN (${players} out)',
 de: 'Feuer INNEN (${players} raus)',
 fr: 'Feu Ã  l\'INTÃ‰RIEUR (${players} Ã©vitez)',
 ja: 'ãƒ•ã‚¡ã‚¤ã‚¢ãƒœãƒ¼ãƒ«ã¯å¯†ç€ (${players}ã¯å¤–ã¸)',
 cn: 'äººç¾¤ç«3 (${players}èº²é¿)',
 ko: 'ë¶ˆ ê°™ì´ë§žê¸° (${players} ëŠ” í”¼í•˜ê¸°)',
 tc: 'äººç¾¤ç«3 (${players} èº²é¿)',
 },
 fireInAvoid: {
 en: 'Fire IN: AVOID!',
 de: 'Feuer INNEN: AUSWEICHEN!',
 fr: 'Feu Ã  l\'INTÃ‰RIEUR : Ã‰VITEZ !',
 ja: 'ãƒ•ã‚¡ã‚¤ã‚¢ãƒœãƒ¼ãƒ«ã¯å¯†ç€: è‡ªåˆ†ã«é›¢ã‚Œ',
 cn: 'èº²é¿äººç¾¤ç«3ï¼',
 ko: 'ë¶ˆ ê°™ì´ë§žê¸°: ë‚˜ëŠ” í”¼í•˜ê¸°',
 tc: 'èº²é¿äººç¾¤ç«3ï¼',
 },
 },
 },
 {
 id: 'UCU Nael Fireball 4',
 type: 'Ability',
 netRegex: { source: 'Ragnarok', id: '26B8', capture: false },
 delaySeconds: 98,
 suppressSeconds: 99999,
 alertText: (pull, _hit, voice) => {
 const tookTw = pull.fireballs[1]?.filter((p) => {
 return pull.fireballs[2]?.includes(p);
 });
 const tookThre = (tookTw ?? []).filter((p) => {
 return pull.fireballs[3]?.includes(p);
 });
 pull.tookThreeFireballs = tookThre.includes(pull.me);
 if (pull.tookThreeFireballs)
 return voice.fireInAvoid();
 },
 infoText: (pull, _hit, voice) => {
 if (!pull.tookThreeFireballs)
 return voice.fireIn();
 },
 run: (pull) => pull.naelFireballCount = 4,
 outputStrings: {
 fireIn: {
 en: 'Fire IN',
 de: 'Feuer INNEN',
 fr: 'Feu Ã  l\'INTÃ‰RIEUR',
 ja: 'ãƒ•ã‚¡ã‚¤ã‚¢ãƒœãƒ¼ãƒ«å¯†ç€',
 cn: 'äººç¾¤ç«4',
 ko: 'ë¶ˆ ê°™ì´ë§žê¸°',
 tc: 'äººç¾¤ç«4',
 },
 fireInAvoid: {
 en: 'Fire IN: AVOID!',
 de: 'Feuer INNEN: AUSWEICHEN!',
 fr: 'Feu Ã  l\'INTÃ‰RIEUR : Ã‰VITEZ !',
 ja: 'ãƒ•ã‚¡ã‚¤ã‚¢ãƒœãƒ¼ãƒ«ã¯å¯†ç€: è‡ªåˆ†ã«é›¢ã‚Œ',
 cn: 'èº²é¿äººç¾¤ç«4ï¼',
 ko: 'ë¶ˆ ê°™ì´ë§žê¸°: ë‚˜ëŠ” í”¼í•˜ê¸°',
 tc: 'èº²é¿äººç¾¤ç«4ï¼',
 },
 },
 },
 {
 id: 'UCU Dragon Tracker',
 type: 'Ability',
 netRegex: {
 source: ['Iceclaw', 'Thunderwing', 'Fang Of Light', 'Tail Of Darkness', 'Firehorn'],
 id: ['26C6', '26C7', '26CA', '26C9', '26C5'],
 },
 condition: (pull, hit) => !(hit.source in pull.seenDragon),
 run: (pull, hit) => {
 pull.seenDragon[hit.source] = true;

 const x = parseFloat(hit.x);
 const y = parseFloat(hit.y);
 const facingTwo = Facings.xyTo8DirNum(x, y, centerXBit, centerYBit);

 pull.naelDragons[facingTwo] = 1;

 if (Object.keys(pull.seenDragon).length !== 5)
 return;

 const answer = findDragonMark(pull.naelDragons);
 if (!answer)
 return;
 pull.naelMarks = answer.marks.map((i) => {
 return Facings.output8Dir[i] ?? 'unknown';
 });
 pull.wideThirdDive = answer.wideThirdDive;
 pull.unsafeThirdMark = answer.unsafeThirdMark;
 if (pull.options.Debug) {
 consol.log(
 `UCU Dragon Tracker${pull.naelMarks.join(', ')}${pull.wideThirdDive ? ' (WIDE)' : ''}`,
 );
 }
 },
 },
 {
 id: 'UCU Nael Ravensbeak',
 type: 'StartsUsing',
 netRegex: { source: 'Nael deus Darnus', id: '26B6' },
 response: Response.tankBusterSwap('alert'),
 },
 {
 id: 'UCU Nael Dragon Placement',
 type: 'Ability',
 netRegex: { source: 'Nael deus Darnus', id: '26B6', capture: false },
 condition: (pull) => pull.naelMarks && !pull.calledNaelDragons,
 durationSeconds: 10,
 infoText: (pull, _hit, voice) => {
 pull.calledNaelDragons = true;
 const param = {
 dive1: voice[pull.naelMarks?.[0] ?? 'unknown'](),
 dive2: voice[pull.naelMarks?.[1] ?? 'unknown'](),
 dive3: voice[pull.naelMarks?.[2] ?? 'unknown'](),
 };
 if (pull.wideThirdDive)
 return voice.marksWide(param);
 return voice.marks(param);
 },
 outputStrings: {
 marks: {
 en: 'Marks: ${dive1}, ${dive2}, ${dive3}',
 de: 'Markierungen : ${dive1}, ${dive2}, ${dive3}',
 fr: 'Marque : ${dive1}, ${dive2}, ${dive3}',
 ja: 'ãƒžãƒ¼ã‚«ãƒ¼: ${dive1}, ${dive2}, ${dive3}',
 cn: 'æ ‡è®°: ${dive1}, ${dive2}, ${dive3}',
 ko: 'ì§•: ${dive1}, ${dive2}, ${dive3}',
 tc: 'æ¨™è¨˜: ${dive1}, ${dive2}, ${dive3}',
 },
 marksWide: {
 en: 'Marks: ${dive1}, ${dive2}, ${dive3} (WIDE)',
 de: 'Markierungen : ${dive1}, ${dive2}, ${dive3} (GROÃŸ)',
 fr: 'Marque : ${dive1}, ${dive2}, ${dive3} (LARGE)',
 ja: 'ãƒžãƒ¼ã‚«ãƒ¼: ${dive1}, ${dive2}, ${dive3} (åºƒ)',
 cn: 'æ ‡è®°: ${dive1}, ${dive2}, ${dive3} (å¤§)',
 ko: 'ì§•: ${dive1}, ${dive2}, ${dive3} (ë„“ìŒ)',
 tc: 'æ¨™è¨˜: ${dive1}, ${dive2}, ${dive3} (å¤§)',
 },
 ...Facings.outputStrings8Dir,
 },
 },
 {
 id: 'UCU Nael Dragon Dive Marker Me',
 type: 'HeadMarker',
 netRegex: { id: '0014' },
 condition: (pull) => !pull.trio,
 alarmText: (pull, hit, voice) => {
 if (hit.target !== pull.me)
 return;
 const facingTwo = pull.naelMarks?.[pull.naelDiveMarkerCount] ?? 'unknown';
 return voice.text({ dir: voice[facingTwo]() });
 },
 outputStrings: {
 text: {
 en: 'Go To ${dir} with marker',
 de: 'Gehe nach ${dir} mit dem Marker',
 fr: 'Allez direction ${dir} avec le marqueur',
 ja: 'ãƒžãƒ¼ã‚«ãƒ¼ä»˜ã„ãŸã¾ã¾${dir}ã¸',
 cn: 'åŽ» ${dir} å¼•å¯¼ä¿¯å†²',
 ko: '${dir}ìœ¼ë¡œ ì´ë™',
 tc: 'åŽ» ${dir} å¼•å°Žä¿¯è¡',
 },
 ...Facings.outputStrings8Dir,
 },
 },
 {
 id: 'UCU Nael Dragon Dive Marker Others',
 type: 'HeadMarker',
 netRegex: { id: '0014' },
 condition: (pull) => !pull.trio,
 infoText: (pull, hit, voice) => {
 if (hit.target === pull.me)
 return;
 const num = pull.naelDiveMarkerCount + 1;
 return voice.text({ num: num, player: pull.party.member(hit.target) });
 },
 outputStrings: {
 text: {
 en: 'Dive #${num}: ${player}',
 de: 'Sturz #${num} : ${player}',
 fr: 'Plongeon #${num} : ${player}',
 ja: 'ãƒ€ã‚¤ãƒ–${num}ç•ªç›®:${player}',
 cn: 'ç¬¬ ${num} æ¬¡ä¿¯å†²ç‚¹: ${player}',
 ko: 'ì¹´íƒˆ ${num}: ${player}',
 tc: 'ç¬¬ ${num} æ¬¡ä¿¯è¡é»ž: ${player}',
 },
 },
 },
 {
 id: 'UCU Nael Dragon Dive Marker Counter',
 type: 'HeadMarker',
 netRegex: { id: '0014', capture: false },
 condition: (pull) => !pull.trio,
 run: (pull) => pull.naelDiveMarkerCount++,
 },

 {
 id: 'UCU Octet Marker Tracking',
 type: 'HeadMarker',
 netRegex: { id: ['0077', '0014', '0029'] },
 condition: (pull) => pull.trio === 'octet',
 run: (pull, hit) => {
 pull.octetMarker.push(hit.target);
 if (pull.octetMarker.length !== 7)
 return;

 const crewRoll = pull.party.details.map((p) => p.name);

 if (crewRoll.length !== 8) {
 consol.error(`Octet error: bad party list size: ${JSON.stringify(crewRoll)}`);
 return;
 }
 const uniqDicts = {};
 for (const sign of pull.octetMarker) {
 uniqDicts[sign] = true;
 if (!crewRoll.includes(sign)) {
 consol.error(`Octet error: could not find ${sign} in ${JSON.stringify(crewRoll)}`);
 return;
 }
 }
 const uniqs = Object.keys(uniqDicts);
 if (uniqs.length !== 7)
 return;

 const remainingMembers = crewRoll.filter((p) => {
 return !pull.octetMarker.includes(p);
 });
 if (remainingMembers.length !== 1) {
 consol.error(
 `Octet error: failed to find player, ${JSON.stringify(crewRoll)} ${
 JSON.stringify(pull.octetMarker)
 }`,
 );
 return;
 }

 pull.lastOctetMarker = remainingMembers[0];
 },
 },
 {
 id: 'UCU Octet Nael Marker',
 type: 'HeadMarker',
 netRegex: { id: '0077' },
 condition: (pull) => pull.trio === 'octet',
 infoText: (pull, hit, voice) => {
 const num = pull.octetMarker.length;
 return voice.text({ num: num, player: pull.party.member(hit.target) });
 },
 outputStrings: {
 text: {
 en: '${num}: ${player} (nael)',
 de: '${num}: ${player} (nael)',
 fr: '${num} : ${player} (nael)',
 ja: '${num}: ${player} (ãƒãƒ¼ãƒ«)',
 cn: '${num}: ${player} (å¥ˆå°”)',
 ko: '${num}: ${player} (ë„¬)',
 tc: '${num}: ${player} (å¥ˆçˆ¾)',
 },
 },
 },
 {
 id: 'UCU Octet Dragon Marker',
 type: 'HeadMarker',
 netRegex: { id: '0014' },
 condition: (pull) => pull.trio === 'octet',
 infoText: (pull, hit, voice) => {
 const num = pull.octetMarker.length;
 return voice.text({ num: num, player: pull.party.member(hit.target) });
 },
 outputStrings: {
 text: {
 en: '${num}: ${player}',
 de: '${num}: ${player}',
 fr: '${num} : ${player}',
 ja: '${num}: ${player}',
 cn: '${num}ï¼š${player}',
 ko: '${num}: ${player}',
 tc: '${num}: ${player}',
 },
 },
 },
 {
 id: 'UCU Octet Baha Marker',
 type: 'HeadMarker',
 netRegex: { id: '0029' },
 condition: (pull) => pull.trio === 'octet',
 infoText: (pull, hit, voice) => {
 const num = pull.octetMarker.length;
 return voice.text({ num: num, player: pull.party.member(hit.target) });
 },
 outputStrings: {
 text: {
 en: '${num}: ${player} (baha)',
 de: '${num}: ${player} (baha)',
 fr: '${num} : ${player} (baha)',
 ja: '${num}: ${player} (ãƒãƒ)',
 cn: '${num}: ${player} (å·´å“ˆ)',
 ko: '${num}: ${player} (ë°”í•˜)',
 tc: '${num}: ${player} (å·´å“ˆ)',
 },
 },
 },
 {
 id: 'UCU Octet Twin Bait',
 type: 'HeadMarker',
 netRegex: { id: '0029', capture: false },
 condition: (pull) => pull.trio === 'octet',
 delaySeconds: 0.5,
 alertText: (pull, _hit, voice) => {
 if (pull.lastOctetMarker === undefined)
 return voice.twinOnUnknown({
 unknown: voice.unknown(),
 dir: voice[Facings.outputFrom8DirNum(pull.octetTwinDir)](),
 });

 return voice.twinOnPlayer({
 player: pull.party.member(pull.lastOctetMarker),
 dir: voice[Facings.outputFrom8DirNum(pull.octetTwinDir)](),
 });
 },
 outputStrings: {
 ...Facings.outputStrings8Dir,
 unknown: Voices.unknown,
 twinOnPlayer: {
 en: '${player} Bait Twin (${dir})',
 de: '${player} KÃ¶der Twintania (${dir})',
 fr: '${player} attire Twintania (${dir})',
 cn: '${player} è¯±å¯¼åŒå¡”å°¼äºš (${dir})',
 ko: '${player} íŠ¸ìœˆíƒ€ë‹ˆì•„ ìœ ë„ (${dir})',
 tc: '${player} èª˜å°Žé›™å¡”å°¼äºž (${dir})',
 },
 twinOnUnknown: {
 en: '${unknown} Bait Twin (${dir})',
 de: '${unknown} KÃ¶der Twintania (${dir})',
 fr: '${unknown} attire Twintania (${dir})',
 cn: '${unknown} è¯±å¯¼åŒå¡”å°¼äºš (${dir})',
 ko: '${unknown} íŠ¸ìœˆíƒ€ë‹ˆì•„ ìœ ë„ (${dir})',
 tc: '${unknown} èª˜å°Žé›™å¡”å°¼äºž (${dir})',
 },
 },
 },
 {
 id: 'UCU Twister Dives',
 type: 'Ability',
 netRegex: { source: 'Twintania', id: '26B2', capture: false },
 suppressSeconds: 2,
 alertText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Twisters',
 de: 'WirbelstÃ¼rme',
 fr: 'Tornades',
 ja: 'ãƒ„ã‚¤ã‚¹ã‚¿ãƒ¼',
 cn: 'æ—‹é£Ž',
 ko: 'íšŒì˜¤ë¦¬',
 tc: 'æ—‹é¢¨',
 },
 },
 },
 {
 id: 'UCU Bahamut Flatten',
 type: 'StartsUsing',
 netRegex: { id: '26D5', source: 'Bahamut Prime' },
 condition: Condition.caresAboutPhysical(),
 response: Response.tankBuster(),
 },
 {
 id: 'UCU Bahamut Gigaflare',
 type: 'StartsUsing',
 netRegex: { id: '26D6', source: 'Bahamut Prime', capture: false },
 alertText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Gigaflare',
 de: 'Gigaflare',
 fr: 'GigaBrasier',
 ja: 'ã‚®ã‚¬ãƒ•ãƒ¬ã‚¢',
 cn: 'åäº¿æ ¸çˆ†',
 ko: 'ê¸°ê°€í”Œë ˆì–´',
 tc: 'åå„„æ ¸çˆ†',
 },
 },
 },
 {
 id: 'UCU Quickmarch Dive Dir',
 type: 'StartsUsing',
 netRegex: { id: '26E1', source: 'Bahamut Prime' },
 condition: (pull) => pull.trio === 'quickmarch',
 alertText: (_pull, hit, voice) => {
 const x = parseFloat(hit.x);
 const y = parseFloat(hit.y);
 const diveFacing = Facings.xyTo8DirOutput(x, y, centerXBit, centerYBit);
 return voice.dive({ dir: voice[diveFacing]() });
 },
 outputStrings: {
 dive: {
 en: '${dir} Dive',
 de: '${dir} Sturzbombe',
 fr: 'PlongÃ©e ${dir}',
 cn: '${dir} ä¿¯å†²',
 ko: '${dir} á„ƒá…¡á„‹á…µá„‡á…³',
 tc: '${dir} ä¿¯è¡',
 },
 ...Facings.outputStrings8Dir,
 },
 },
 {
 id: 'UCU P3 Nael Collect',
 type: 'StartsUsing',
 netRegex: { id: '26C3', source: 'Nael deus Darnus' },
 condition: (pull) => pull.trio === 'quickmarch',
 run: (pull, hit) => pull.trioSourceIds.nael = parseInt(hit.sourceId, 16),
 },
 {
 id: 'UCU P3 Bahamut Collect',
 type: 'StartsUsing',
 netRegex: { id: '26E1', source: 'Bahamut Prime' },
 condition: (pull) => pull.trio === 'quickmarch',
 run: (pull, hit) => pull.trioSourceIds.bahamut = parseInt(hit.sourceId, 16),
 },
 {
 id: 'UCU P3 Twintania Collect',
 type: 'StartsUsing',
 netRegex: { id: '26B2', source: 'Twintania' },
 condition: (pull) => pull.trio === 'quickmarch',
 run: (pull, hit) => pull.trioSourceIds.twin = parseInt(hit.sourceId, 16),
 },
 {
 id: 'UCU Blackfire Party Dir',
 type: 'ActorSetPos',
 netRegex: { capture: true },
 condition: (pull, hit) => {
 if (pull.trio !== 'blackfire')
 return false;
 if (parseInt(hit.id, 16) !== pull.trioSourceIds.nael)
 return false;

 return true;
 },
 suppressSeconds: 9999,
 alertText: (_pull, hit, voice) => {
 const placeX = parseFloat(hit.x);
 const placeY = parseFloat(hit.y);
 const naelFacingVoice = Facings.xyTo8DirOutput(placeX, placeY, centerXBit, centerYBit);
 return voice.naelPosition({ dir: voice[naelFacingVoice]() });
 },
 outputStrings: {
 naelPosition: {
 en: 'Nael is ${dir}',
 de: 'Nael ist im ${dir}',
 fr: 'Nael est vers ${dir}',
 cn: 'å¥ˆå°”åœ¨ ${dir}',
 ko: 'ë„¬ ${dir}',
 tc: 'å¥ˆçˆ¾åœ¨ ${dir}',
 },
 ...Facings.outputStrings8Dir,
 },
 },
 {
 id: 'UCU Megaflare Stack Me',
 type: 'HeadMarker',
 netRegex: { id: '0027' },
 condition: Condition.targetIsYou(),
 alertText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Megaflare Stack',
 de: 'Megaflare Sammeln',
 fr: 'MÃ©gabrasier, packez-vous',
 ja: 'ãƒ¡ã‚¬ãƒ•ãƒ¬ã‚¢é ­å‰²ã‚Š',
 cn: 'åˆ†æ‘Šç™¾ä¸‡æ ¸çˆ†',
 ko: 'ê¸°ê°€í”Œë ˆì–´ ì‰ì–´',
 tc: 'åˆ†æ”¤ç™¾è¬æ ¸çˆ†',
 },
 },
 },
 {
 id: 'UCU Megaflare Stack Tracking',
 type: 'HeadMarker',
 netRegex: { id: '0027' },
 run: (pull, hit) => pull.megaStack.push(hit.target),
 },
 {
 id: 'UCU Megaflare Tower',
 type: 'HeadMarker',
 netRegex: { id: '0027', capture: false },
 infoText: (pull, _hit, voice) => {
 if (pull.trio !== 'blackfire' && pull.trio !== 'octet' || pull.megaStack.length !== 4)
 return;

 if (pull.megaStack.includes(pull.me))
 return;

 if (pull.trio === 'blackfire')
 return voice.blackfireTower();

 if (pull.lastOctetMarker === undefined || pull.lastOctetMarker === pull.me)
 return voice.octetTowerPlusTwin();

 return voice.octetTower();
 },
 tts: (pull, _hit, voice) => {
 if (pull.trio !== 'blackfire' && pull.trio !== 'octet' || pull.megaStack.length !== 4)
 return;

 if (!pull.megaStack.includes(pull.me))
 return voice.towerTTS();
 },
 outputStrings: {
 blackfireTower: {
 en: 'Tower, bait hypernova',
 de: 'Turm, Hypernova kÃ¶dern',
 fr: 'Tour, attirez la Supernova',
 ja: 'ã‚¿ãƒ¯ãƒ¼ã‚„ã‚¹ãƒ¼ãƒ‘ãƒ¼ãƒŽãƒ´ã‚¡',
 cn: 'è¸©å¡”, å¼•å¯¼è¶…æ–°æ˜Ÿ',
 ko: 'ì´ˆì‹ ì„± í”¼í•˜ê³  ê¸°ë‘¥ ë°Ÿê¸°',
 tc: 'è¸©å¡”, å¼•å°Žè¶…æ–°æ˜Ÿ',
 },
 octetTowerPlusTwin: {
 en: 'Bait Twin, then tower',
 de: 'Twintania in Turm locken',
 fr: 'Attirez GÃ©mellia, puis tour',
 ja: 'ã‚¿ãƒ‹ã‚¢ãƒ€ã‚¤ãƒ–ã‚„ã‚¿ãƒ¯ãƒ¼',
 cn: 'å¼•å¯¼åŒå¡”, è¸©å¡”',
 ko: 'íŠ¸ìœˆíƒ€ë‹ˆì•„ ìœ ë„ í›„ ê¸°ë‘¥ ë°Ÿê¸°',
 tc: 'å¼•å°Žé›™å¡”, è¸©å¡”',
 },
 octetTower: {
 en: 'Get in a far tower',
 de: 'Geh in entfernten Turm',
 fr: 'Aller dans une tour lointaine',
 ja: 'é ã„ã‚¿ãƒ¯ãƒ¼',
 cn: 'è¸©è¿œå¤„çš„å¡”',
 ko: 'ê¸°ë‘¥ ë°Ÿê¸°',
 tc: 'è¸©é è™•çš„å¡”',
 },
 towerTTS: {
 en: 'tower',
 de: 'Turm',
 fr: 'Tour',
 ja: 'ã‚¿ãƒ¯ãƒ¼',
 cn: 'è¸©å¡”',
 ko: 'ê¸°ë‘¥',
 tc: 'è¸©å¡”',
 },
 },
 },
 {
 id: 'UCU Megaflare Twin Tower',
 type: 'HeadMarker',
 netRegex: { id: '0027', capture: false },
 delaySeconds: 0.5,
 suppressSeconds: 1,
 infoText: (pull, _hit, voice) => {
 if (pull.trio !== 'blackfire' && pull.trio !== 'octet' || pull.megaStack.length !== 4)
 return;
 if (pull.lastOctetMarker === undefined || pull.lastOctetMarker === pull.me)
 return;

 const twins = pull.party.member(pull.lastOctetMarker);
 if (pull.megaStack.includes(pull.lastOctetMarker))
 return voice.twinHasMegaflare({ player: twins });
 return voice.twinHasTower({ player: twins });
 },
 tts: null,
 outputStrings: {
 twinHasMegaflare: {
 en: '${player} (twin) has megaflare',
 de: '${player} (Twin) hat Megaflare',
 fr: '${player} (GÃ©mellia) a mÃ©gabrasier',
 ja: '${player} (ãƒ„ã‚¤ãƒ³ã‚¿ãƒ‹ã‚¢) ãƒ¡ã‚¬é ­å‰²ã‚Š',
 cn: 'åŒå¡”ä¿¯å†²ç‚¹åˆ†æ‘Š ï¼ˆ${player})',
 ko: '${player} (íŠ¸ìœˆ ì§• ëŒ€ìƒìž) => ì‰ì–´',
 tc: 'é›™å¡”ä¿¯è¡é»žåˆ†æ”¤ ï¼ˆ${player})',
 },
 twinHasTower: {
 en: '${player} (twin) needs tower',
 de: '${player} (Twin) braucht einen Turm',
 fr: '${player} (GÃ©mellia) ont besoin d\'une tour',
 ja: '${player} (ãƒ„ã‚¤ãƒ³ã‚¿ãƒ‹ã‚¢) å¡”ã‚’è¸ã‚€',
 cn: 'åŒå¡”ä¿¯å†²ç‚¹è¸©å¡”ï¼ˆ${player}ï¼‰',
 ko: '${player} (íŠ¸ìœˆ ì§• ëŒ€ìƒìž) => ê¸°ë‘¥',
 tc: 'é›™å¡”ä¿¯è¡é»žè¸©å¡”ï¼ˆ${player}ï¼‰',
 },
 },
 },
 {
 id: 'UCU Heavensfall Nael Spot',
 type: 'ActorSetPos',
 netRegex: { capture: true },
 condition: (pull, hit) => {
 if (pull.trio !== 'heavensfall')
 return false;

 if (!Object.values(pull.trioSourceIds).includes(parseInt(hit.id, 16)))
 return false;

 if (Object.keys(pull.combatantData).length >= 3)
 return false;

 return true;
 },
 preRun: (pull, hit) => {
 pull.combatantData[parseInt(hit.id, 16)] = hit;
 },
 alertText: (pull, _hit, voice) => {
 if (Object.keys(pull.combatantData).length < 3)
 return;

 let naelTurnAmount;
 let bahamutTurnAmount;
 let twinTurnAmount;
 let naelPlace = 'unknown';
 for (const mobs of Object.values(pull.combatantData)) {
 const mobTurnAmount = placeToTurnAmount(mobs);
 const mobCode = parseInt(mobs.id, 16);
 if (mobCode === pull.trioSourceIds.nael)
 naelTurnAmount = mobTurnAmount;
 else if (mobCode === pull.trioSourceIds.bahamut)
 bahamutTurnAmount = mobTurnAmount;
 else if (mobCode === pull.trioSourceIds.twin)
 twinTurnAmount = mobTurnAmount;
 }
 if (naelTurnAmount === undefined || bahamutTurnAmount === undefined || twinTurnAmount === undefined)
 return;
 pull.heavensfallNaelAngle = naelTurnAmount;
 if (naelTurnAmount >= 0 && bahamutTurnAmount >= 0 && twinTurnAmount >= 0) {
 if (isClockwis(naelTurnAmount, bahamutTurnAmount))
 naelPlace = isClockwis(naelTurnAmount, twinTurnAmount) ? 'left' : 'middle';
 else
 naelPlace = isClockwis(naelTurnAmount, twinTurnAmount) ? 'middle' : 'right';
 }
 return voice.naelPosition({ dir: voice[naelPlace]() });
 },
 outputStrings: {
 naelPosition: {
 en: '${dir} Nael',
 de: '${dir} Nael',
 fr: 'Nael ${dir}',
 cn: '${dir} å¥ˆå°”',
 ko: 'ë„¬ ${dir}',
 tc: '${dir} å¥ˆçˆ¾',
 },
 left: Voices.left,
 middle: Voices.middle,
 right: Voices.right,
 unknown: Voices.unknown,
 },
 },
 {
 id: 'UCU Heavensfall Tower Spot',
 type: 'StartsUsingExtra',
 netRegex: { id: '26DF', capture: true },
 condition: (pull) => {
 return pull.triggerSetConfig.heavensfallTowerPosition !== 'disabled' &&
 pull.trio === 'heavensfall';
 },
 preRun: (pull, hit) => {
 pull.heavensfallTowerSpots.push(hit);
 },
 durationSeconds: 8,
 infoText: (pull, _hit, voice) => {
 if (pull.heavensfallTowerSpots.length < 8)
 return;

 const naelTurnAmount = pull.heavensfallNaelAngle;
 if (naelTurnAmount === undefined)
 return;
 const wantedSpot = parseInt(pull.triggerSetConfig.heavensfallTowerPosition);
 const pillars = pull.heavensfallTowerSpots.sort((l, r) => placeToTurnAmount(l) - placeToTurnAmount(r));

 const pillarsTable = pillars.map((t) =>
 Facings.xyTo16DirNum(parseFloat(t.x), parseFloat(t.y), centerXBit, centerYBit)
 );

 let naelSpot = pillars.findIndex((t) => placeToTurnAmount(t) >= naelTurnAmount);

 if (naelSpot < 0)
 naelSpot += 8;

 const pillarFacing = pillarsTable[(wantedSpot + naelSpot) % 8];

 const myPillarFacing = pillarFacing !== undefined
 ? Facings.output16Dir[pillarFacing] ?? 'unknown'
 : 'unknown';

 return voice.tower({
 dir: voice[myPillarFacing](),
 });
 },
 outputStrings: {
 tower: {
 en: 'Tower: ${dir}',
 de: 'Turm: ${dir}',
 fr: 'Tour : ${dir}',
 cn: 'å¡”: ${dir}',
 ko: 'ê¸°ë‘¥: ${dir}',
 tc: 'å¡”: ${dir}',
 },
 ...Facings.outputStrings16Dir,
 },
 },
 {
 id: 'UCU Earthshaker Me',
 type: 'HeadMarker',
 netRegex: { id: '0028' },
 condition: Condition.targetIsYou(),
 response: Response.earthshaker('alarm'),
 },
 {
 id: 'UCU Earthshaker Tracking',
 type: 'HeadMarker',
 netRegex: { id: '0028' },
 run: (pull, hit) => pull.shakers.push(hit.target),
 },
 {
 id: 'UCU Earthshaker Not Me',
 type: 'HeadMarker',
 netRegex: { id: '0028', capture: false },
 alertText: (pull, _hit, voice) => {
 if (pull.trio !== 'quickmarch')
 return;
 if (pull.shakers.length !== 3)
 return;
 if (pull.role === 'tank')
 return voice.quickmarchTankTether();
 },
 infoText: (pull, _hit, voice) => {
 if (pull.trio === 'quickmarch') {
 if (pull.shakers.length !== 3)
 return;
 if (!pull.shakers.includes(pull.me) && pull.role !== 'tank')
 return voice.quickmarchNotOnYou();
 } else if (pull.trio === 'tenstrike') {
 if (pull.shakers.length === 4 && !pull.shakers.includes(pull.me))
 return voice.tenstrikeNotOnYou();
 }
 },
 run: (pull) => {
 if (pull.trio === 'tenstrike' && pull.shakers.length === 4)
 pull.shakers = [];
 },
 outputStrings: {
 quickmarchTankTether: {
 en: 'Pick up tether',
 de: 'Verbindung holen',
 fr: 'Prenez un lien',
 ja: 'ãƒ†ãƒ³ãƒšã‚¹ãƒˆã‚¦ã‚£ãƒ³ã‚°ç·š',
 cn: 'æŽ¥çº¿',
 ko: 'ì¤„ ê°€ë¡œì±„ê¸°',
 tc: 'æŽ¥ç·š',
 },
 quickmarchNotOnYou: {
 en: 'No shaker; stack south.',
 de: 'Kein ErdstoÃŸ; im SÃ¼den sammeln',
 fr: 'Pas de Secousse; packez-vous au Sud.',
 ja: 'ã‚·ã‚§ã‚¤ã‚«ãƒ¼ãªã„ï¼›é ­å‰²ã‚Šã§å—',
 cn: 'æ— ç‚¹åï¼Œæ­£ä¸‹æ–¹åˆ†æ‘Š',
 ko: 'ì§• ì—†ìŒ, ëª¨ì—¬ì„œ ì‰ì–´',
 tc: 'ç„¡é»žåï¼Œå—é¢åˆ†æ”¤',
 },
 tenstrikeNotOnYou: {
 en: 'Stack on safe spot',
 de: 'In Sicherheit sammeln',
 fr: 'Packez-vous au point safe',
 ja: 'å®‰ç½®ã¸é›†åˆ',
 cn: 'å®‰å…¨ç‚¹é›†åˆ',
 ko: 'ì•ˆì „ìž¥ì†Œì— ëª¨ì´ê¸°',
 tc: 'å®‰å…¨é»žé›†åˆ',
 },
 },
 },
 {
 id: 'UCU Grand Octet Run & Rotate',
 type: 'ActorSetPos',
 netRegex: { capture: true },
 condition: (pull, hit) => {
 if (pull.trio !== 'octet')
 return false;

 if (!Object.values(pull.trioSourceIds).includes(parseInt(hit.id, 16)))
 return false;

 if (Object.keys(pull.combatantData).length >= 3)
 return false;

 return true;
 },
 preRun: (pull, hit) => {
 pull.combatantData[parseInt(hit.id, 16)] = hit;
 },
 alertText: (pull, _hit, voice) => {
 if (Object.keys(pull.combatantData).length < 3)
 return;

 let naelFacingSpot;
 let bahaFacingSpot;

 for (const mobs of Object.values(pull.combatantData)) {
 const mobCode = parseInt(mobs.id, 16);
 const mobFacingSpot = Facings.xyTo8DirNum(
 parseFloat(mobs.x),
 parseFloat(mobs.y),
 centerXBit,
 centerYBit,
 );
 if (mobCode === pull.trioSourceIds.nael)
 naelFacingSpot = mobFacingSpot;
 else if (mobCode === pull.trioSourceIds.bahamut)
 bahaFacingSpot = mobFacingSpot;
 else if (mobCode === pull.trioSourceIds.twin)
 pull.octetTwinDir = mobFacingSpot;
 }

 if (naelFacingSpot === undefined || bahaFacingSpot === undefined)
 return;

 let rotationSpotModifier;
 let rotationPaths;

 const bahaVoiceWord = Facings.output8Dir[bahaFacingSpot];
 const cardinalFacings = Facings.outputCardinalDir;
 if (bahaVoiceWord === undefined)
 return;
 if (cardinalFacings.includes(bahaVoiceWord)) {
 rotationSpotModifier = -1;
 rotationPaths = 'counterclockwise';
 } else {
 rotationSpotModifier = 1;
 rotationPaths = 'clockwise';
 }

 let crewOpenSpot = bahaFacingSpot >= 4 ? bahaFacingSpot - 4 : bahaFacingSpot + 4;
 if (naelFacingSpot === crewOpenSpot) {
 crewOpenSpot += rotationSpotModifier;
 if (crewOpenSpot === -1) {
 crewOpenSpot = 7;
 } else if (crewOpenSpot === 8) {
 crewOpenSpot = 0;
 }
 }
 const crewOpenFacing = Facings.output8Dir[crewOpenSpot] ?? 'unknown';

 if (crewOpenFacing === undefined || rotationPaths === undefined)
 return;
 return voice.grandOctet({
 startDir: voice[crewOpenFacing](),
 path: voice[rotationPaths](),
 });
 },
 outputStrings: {
 grandOctet: {
 en: 'Bait dash, go ${startDir}, rotate ${path}',
 de: 'Ansturm kÃ¶dern, gehe nach ${startDir}, rotiere ${path}',
 fr: 'Attirez le dash, allez ${startDir}, tournez ${path}',
 cn: 'è¯±å¯¼ä¿¯å†², åŽ» ${startDir}, ${path} è½¬',
 ko: 'ëŒì§„ ìœ ë„, ${startDir}ìª½ìœ¼ë¡œ, ${path}',
 tc: 'èª˜å°Žä¿¯è¡, åŽ» ${startDir}, ${path} è½‰',
 },
 clockwise: Voices.clockwise,
 counterclockwise: Voices.counterclockwise,
 ...Facings.outputStrings8Dir,
 },
 },

 {
 id: 'UCU Morn Afah',
 type: 'StartsUsing',
 netRegex: { id: '26EC', source: 'Bahamut Prime' },
 preRun: (pull) => pull.mornAfahCount++,
 alertText: (pull, hit, voice) => {
 if (hit.target === pull.me)
 return voice.mornAfahYou({ num: pull.mornAfahCount });
 return voice.mornAfahPlayer({
 num: pull.mornAfahCount,
 player: pull.party.member(hit.target),
 });
 },
 outputStrings: {
 mornAfahYou: {
 en: 'Morn Afah #${num} (YOU)',
 de: 'Morn Afah #${num} (DU)',
 fr: 'Morn Afah #${num} (VOUS)',
 ja: 'ãƒ¢ãƒ¼ãƒ³ãƒ»ã‚¢ãƒ•ã‚¡ãƒ¼${num}å›ž (è‡ªåˆ†)',
 cn: 'æ— å°½é¡¿æ‚Ÿ #${num}',
 ko: 'ëª¬ ì•„íŒŒ ${num} (ë‚˜ì—ê²Œ)',
 tc: 'ç„¡ç›¡é “æ‚Ÿ #${num}',
 },
 mornAfahPlayer: {
 en: 'Morn Afah #${num} (${player})',
 de: 'Morn Afah #${num} (${player})',
 fr: 'Morn Afah #${num} (${player})',
 ja: 'ãƒ¢ãƒ¼ãƒ³ãƒ»ã‚¢ãƒ•ã‚¡ãƒ¼${num}å›ž (${player})',
 cn: 'æ— å°½é¡¿æ‚Ÿ #${num} (${player})',
 ko: 'ëª¬ ì•„íŒŒ ${num} (${player})',
 tc: 'ç„¡ç›¡é “æ‚Ÿ #${num} (${player})',
 },
 },
 },
 {
 id: 'UCU Akh Morn',
 type: 'StartsUsing',
 netRegex: { id: '26EA', source: 'Bahamut Prime', capture: false },
 preRun: (pull) => {
 pull.akhMornCount++;
 },
 infoText: (pull, _hit, voice) => voice.text({ num: pull.akhMornCount }),
 outputStrings: {
 text: {
 en: 'Akh Morn #${num}',
 de: 'Akh Morn #${num}',
 fr: 'Akh Morn #${num}',
 ja: 'ã‚¢ã‚¯ãƒ»ãƒ¢ãƒ¼ãƒ³ #${num}',
 cn: 'æ­»äº¡è½®å›ž #${num}',
 ko: 'ì•„í¬ ëª¬ ${num}',
 tc: 'æ­»äº¡è¼ªè¿´ #${num}',
 },
 },
 },
 {
 id: 'UCU Exaflare Direction',
 type: 'StartsUsingExtra',
 netRegex: { id: '26F0', capture: true },
 suppressSeconds: 20,
 infoText: (_pull, hit, voice) => {
 const towardsFacingDigit = Facings.hdgTo8DirNum(parseFloat(hit.heading));
 const towardsFacing = Facings.outputFrom8DirNum(towardsFacingDigit);
 const openFacing = Facings.outputFrom8DirNum((towardsFacingDigit + 4) % 8);
 return voice.text(
 {
 dir1: voice[openFacing](),
 dir2: voice[towardsFacing](),
 },
 );
 },
 tts: (_pull, hit, voice) => {
 const towardsFacingDigit = Facings.hdgTo8DirNum(parseFloat(hit.heading));
 const towardsFacing = Facings.outputFrom8DirNum(towardsFacingDigit);
 const openFacing = Facings.outputFrom8DirNum((towardsFacingDigit + 4) % 8);
 return voice.tts(
 {
 dir1: voice[openFacing](),
 dir2: voice[towardsFacing](),
 },
 );
 },
 outputStrings: {
 ...Facings.outputStrings8Dir,
 text: {
 en: 'Exaflares ${dir1} -> ${dir2}',
 de: 'Exaflares ${dir1} -> ${dir2}',
 fr: 'Brasiers ${dir1} -> ${dir2}',
 cn: 'ç™¾äº¬æ ¸çˆ† ${dir1} -> ${dir2}',
 ko: 'ì—‘ì‚¬í”Œë ˆì–´ ${dir1} -> ${dir2}',
 tc: 'ç™¾äº¬ç«å…‰ ${dir1} -> ${dir2}',
 },
 tts: {
 en: 'Exaflares ${dir1} towards ${dir2}',
 de: 'Exaflares ${dir1} nach ${dir2}',
 fr: 'Brasiers ${dir1} vers ${dir2}',
 cn: 'ç™¾äº¬æ ¸çˆ† ä»Ž ${dir1} åˆ° ${dir2}',
 ko: 'ì—‘ì‚¬í”Œë ˆì–´ ${dir1}ì—ì„œ ${dir2}',
 tc: 'ç™¾äº¬ç«å…‰ å¾ž ${dir1} åˆ° ${dir2}',
 },
 },
 },
 {
 id: 'UCU Morn Afah Enrage Spread Warning',
 type: 'StartsUsing',
 netRegex: { id: '26ED', source: 'Bahamut Prime', capture: false },
 alarmText: (_pull, _hit, voice) => voice.text(),
 outputStrings: {
 text: {
 en: 'Spread (Enrage)',
 de: 'Verteilen (Finalangriff)',
 fr: 'Dispersion (Enrage)',
 cn: 'åˆ†æ•£ (ç‹‚æš´)',
 ko: 'ì‚°ê°œ (ì „ë©¸ê¸°)',
 tc: 'åˆ†æ•£ (ç‹‚æš´)',
 },
 },
 },
];

defineDuty({
  id: 'TheUnendingCoilOfBahamutUltimate',
  name: 'UCOB - The Unending Coil',
  category: 'Ultimate',
  zoneId: 733,
  boss: '',
  center: { x: 0, y: 0 },
  state: function () {
    return {
 partyList: {},
 currentPhase: 2,
 fireDebuff: false,
 iceDebuff: false,
 thunderDebuffs: [],
 thunderOnYou: false,
 naelFireballCount: 0,
 fireballs: {
 1: [],
 2: [],
 3: [],
 4: [],
 },
 seenDragon: {},
 naelDragons: [0, 0, 0, 0, 0, 0, 0, 0],
 calledNaelDragons: false,
 wideThirdDive: false,
 unsafeThirdMark: false,
 naelDiveMarkerCount: 0,
 trioSourceIds: {},
 combatantData: {},
 heavensfallTowerSpots: [],
 shakers: [],
 megaStack: [],
 octetMarker: [],
 octetTwinDir: -1,
 exaflareCount: 0,
 akhMornCount: 0,
 mornAfahCount: 0,
      triggerSetConfig: { heavensfallTowerPosition: 'disabled' },
    };
  },
  mechanics: ucobTimelineCues.concat(ucobCues).map(function (t) { return raws(t); }),
});
