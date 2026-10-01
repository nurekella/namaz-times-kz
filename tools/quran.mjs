// Builds quran/*.txt (one verse per line, 6236 lines, in order) and quran/suras.json from downloaded sources.
// Usage: node tools/quran.mjs <download dir>
// The download dir must hold:
//   ar.txt  https://tanzil.net/pub/download/index.php?quranType=uthmani&outType=txt-2&agree=true  (Tanzil Uthmani, CC BY 3.0, verbatim)
//   ru.txt  https://tanzil.net/trans/ru.kuliev       (Elmir Kuliev)
//   en.txt  https://tanzil.net/trans/en.sahih        (Saheeh International)
//   tr.json https://api.alquran.cloud/v1/quran/en.transliteration
//   kk.json {sura: result} from https://quranenc.com/api/v1/translation/sura/kazakh_altai/{1..114} (Khalifa Altai)
import fs from 'fs';
import path from 'path';

const src = process.argv[2];
const out = path.join(path.dirname(new URL(import.meta.url).pathname.replace(/^\/(\w:)/, '$1')), '..', 'quran');
fs.mkdirSync(out, { recursive: true });

// Tanzil "sura|aya|text" files; comments and blank lines are skipped.
const tanzil = f => fs.readFileSync(path.join(src, f), 'utf8').split('\n').filter(l => /^\d+\|\d+\|/.test(l)).map(l => l.split('|').slice(2).join('|').trim());
const clean = t => t.replace(/\s+/g, ' ').trim();
const kk = JSON.parse(fs.readFileSync(path.join(src, 'kk.json'), 'utf8'));
const tr = JSON.parse(fs.readFileSync(path.join(src, 'tr.json'), 'utf8')).data.surahs;

const files = {
  ar: tanzil('ar.txt'),
  ru: tanzil('ru.txt'),
  en: tanzil('en.txt'),
  kk: Object.keys(kk).sort((a, b) => a - b).flatMap(s => kk[s].map(a => clean(a.translation))),
  tl: tr.flatMap(s => s.ayahs.map(a => clean(a.text))),
};
for (const [k, lines] of Object.entries(files)) {
  if (lines.length !== 6236) throw new Error(`${k}: ${lines.length} verses`);
  fs.writeFileSync(path.join(out, `${k}.txt`), lines.join('\n'));
}

const kkNames = `Фатиха Бақара Әли_Имран Ниса Мәида Әнғам Ағраф Әнфәл Тәубе Юнус Һуд Юсуф Рағд Ибраһим Хижр Нахл Исра Кәһф Мәриям Таһа Әнбия Хаж Мүминун Нұр Фурқан Шұғара Нәміл Қасас Анкабут Рум Лұқман Сәжде Ахзаб Сәбә Фатыр Ясин Саффат Сад Зүмәр Ғафир Фуссилат Шура Зухруф Духан Жәсия Ахқаф Мұхаммед Фатх Хужурат Қаф Зәрият Тур Нәжм Қамар Рахман Уақиға Хадид Мүжәдәлә Хашр Мүмтахина Сафф Жұма Мүнафиқун Тәғабун Талақ Тахрим Мүлк Қалам Хаққа Мағариж Нұх Жын Мүззәммил Мүддәссир Қиямет Инсан Мурсалат Нәбә Нәзиғат Әбәсә Тәкуир Инфитар Мутаффифин Иншиқақ Буруж Тариқ Ағла Ғашия Фәжр Бәләд Шәмс Ләйл Духа Инширах Тин Алақ Қадр Бәййина Зілзала Адият Қариға Тәкәсүр Аср Һумәза Фил Құрайыш Мағұн Кәусар Кафирун Наср Мәсәд Ихлас Фәләқ Нас`;
const ruNames = `Аль-Фатиха|Аль-Бакара|Аль Имран|Ан-Ниса|Аль-Маида|Аль-Анам|Аль-Араф|Аль-Анфаль|Ат-Тауба|Юнус|Худ|Юсуф|Ар-Раад|Ибрахим|Аль-Хиджр|Ан-Нахль|Аль-Исра|Аль-Кахф|Марьям|Та Ха|Аль-Анбия|Аль-Хадж|Аль-Муминун|Ан-Нур|Аль-Фуркан|Аш-Шуара|Ан-Намль|Аль-Касас|Аль-Анкабут|Ар-Рум|Лукман|Ас-Саджда|Аль-Ахзаб|Саба|Фатыр|Йа Син|Ас-Саффат|Сад|Аз-Зумар|Гафир|Фуссилят|Аш-Шура|Аз-Зухруф|Ад-Духан|Аль-Джасия|Аль-Ахкаф|Мухаммад|Аль-Фатх|Аль-Худжурат|Каф|Аз-Зарийат|Ат-Тур|Ан-Наджм|Аль-Камар|Ар-Рахман|Аль-Вакиа|Аль-Хадид|Аль-Муджадила|Аль-Хашр|Аль-Мумтахана|Ас-Сафф|Аль-Джумуа|Аль-Мунафикун|Ат-Тагабун|Ат-Талак|Ат-Тахрим|Аль-Мульк|Аль-Калам|Аль-Хакка|Аль-Мааридж|Нух|Аль-Джинн|Аль-Муззаммиль|Аль-Муддассир|Аль-Кияма|Аль-Инсан|Аль-Мурсалят|Ан-Наба|Ан-Назиат|Абаса|Ат-Таквир|Аль-Инфитар|Аль-Мутаффифин|Аль-Иншикак|Аль-Бурудж|Ат-Тарик|Аль-Аля|Аль-Гашия|Аль-Фаджр|Аль-Балад|Аш-Шамс|Аль-Лейл|Ад-Духа|Аш-Шарх|Ат-Тин|Аль-Алак|Аль-Кадр|Аль-Баййина|Аз-Зальзаля|Аль-Адият|Аль-Кариа|Ат-Такасур|Аль-Аср|Аль-Хумаза|Аль-Филь|Курайш|Аль-Маун|Аль-Каусар|Аль-Кафирун|Ан-Наср|Аль-Масад|Аль-Ихлас|Аль-Фалак|Ан-Нас`.split('|');
const kkList = kkNames.split(" ").map(n => n.replace("_", " "));
// Arabic name, English transliteration and Meccan/Medinan come from the alquran.cloud metadata.
const suras = tr.map((s, i) => ({
  n: s.ayahs.length, mecca: s.revelationType === 'Meccan', ar: s.name.replace(/^سُورَةُ /, ''),
  kk: kkList[i], ru: ruNames[i], en: s.englishName,
}));
if (kkList.length !== 114 || ruNames.length !== 114) throw new Error(`names: ${kkList.length}/${ruNames.length}`);
fs.writeFileSync(path.join(out, 'suras.json'), JSON.stringify(suras, null, 0).replace(/\},\{/g, '},\n{'));
console.log('ok', Object.keys(files).join(' '));
