using System.Collections.Generic;

namespace Temnorid.Escape
{
	/// <summary>
	/// Every line of text the player reads, in one place (Ukrainian). A second language is a second table; no
	/// other script holds a sentence.
	/// </summary>
	public static class Loc
	{
		static readonly Dictionary<string, string> Table = new Dictionary<string, string>
		{
			// Title and menus.
			{ "title", "Темнорід: Втеча" },
			{ "subtitle", "В'язниця сторожової вежі" },
			{ "menu.new", "Нова гра" },
			{ "menu.continue", "Продовжити" },
			{ "menu.settings", "Налаштування" },
			{ "menu.quit", "Вихід" },
			{ "menu.corrupt", "Збереження пошкоджене. Почніть нову гру." },
			{ "menu.demo", "Демо: ніяких даних Lords не змінюється." },
			{ "intro", "Ваша місія провалилася. Сільський патруль упіймав вас біля комори. Спорядження забрали. На світанку вас приведуть до сільського старости. Знайдіть вихід, поки охоронець не повернувся." },
			{ "intro.go", "Почати" },

			// HUD and screens.
			{ "inventory", "Інвентар" },
			{ "journal", "Щоденник" },
			{ "hint", "Підказка" },
			{ "pause", "Пауза" },
			{ "resume", "Продовжити" },
			{ "restart", "Повторити" },
			{ "checkpoint", "З контрольної точки" },
			{ "menu", "До меню" },
			{ "back", "Назад" },
			{ "confirm", "Підтвердити" },
			{ "close", "Закрити" },
			{ "combine", "З'єднати половинки ключа" },
			{ "empty", "Порожньо" },
			{ "clues", "Знайдені підказки" },
			{ "no.clues", "Поки що нічого." },
			{ "controls", "WASD або стрілки: рух. Shift: швидше. E: дія. Tab або I: інвентар. J: щоденник. Esc: пауза." },
			{ "controls.touch", "Лівий палець: рух. Кнопка «Дія»: взяти, використати, сховатися. «Біг»: швидше. II: пауза. Нижні вкладки: інвентар і щоденник." },
			{ "place.prison", "В'язниця" },
			{ "touch.act", "Дія" },
			{ "touch.run", "Біг" },
			{ "net.with", "Разом з: {0}" },
			{ "net.alone", "Чекаємо на другого в'язня" },
			{ "volume.master", "Загальна гучність" },
			{ "volume.sfx", "Ефекти" },
			{ "fullscreen", "Повний екран" },
			{ "caught", "Спіймали!" },
			{ "caught.sub", "Повертаємося до контрольної точки. Нічого не втрачено." },
			{ "escaped", "Втеча вдалася" },
			{ "time", "Час" },
			{ "detections", "Викриттів" },
			{ "replay", "Повторити" },
			{ "lords.demo", "Демо: в'язень утік. У Lords захоплений агент зможе знову стати доступним після того, як авторитетний результат втечі буде прийнято. Жодних даних Lords не змінено." },
			{ "lords.mission", "Втікач вільний. Результат місії все одно визначає Lords." },
			{ "village", "Село" },
			{ "reason.scouting", "розвідка" },
			{ "reason.theft", "крадіжка" },

			// Objectives.
			{ "obj.cell", "Знайти вихід із камери" },
			{ "obj.guard", "Уникай охоронця і поверни свої речі" },
			{ "obj.key", "Поверни свої речі" },
			{ "obj.gate", "Відкрий службові ворота" },
			{ "obj.out", "Вийди надвір" },

			// Prompts.
			{ "verb.examine", "Оглянути" },
			{ "verb.take", "Взяти" },
			{ "verb.use", "Використати" },
			{ "verb.hide", "Сховатися" },
			{ "verb.leave", "Вийти з укриття" },
			{ "verb.pull", "Потягнути" },
			{ "prompt", "E — {0} {1}" },
			{ "o.cell_bed", "ліжко" },
			{ "o.cell_wall_clue", "подряпини на стіні" },
			{ "o.cell_cup", "глиняну чашку" },
			{ "o.cell_wire", "зігнутий дріт" },
			{ "o.cell_latch", "защіпку" },
			{ "o.corridor_hiding_spot", "бочки" },
			{ "o.storeroom_ledger", "облікову книгу" },
			{ "o.storeroom_shelf", "полиці" },
			{ "o.records_cabinet", "шафу з записами" },
			{ "o.confiscated_pouch", "конфісковану торбинку" },
			{ "o.workbench_key_piece", "частину ключа на верстаку" },
			{ "o.service_gate_plaque", "табличку" },
			{ "o.service_gate_keyhole", "замок воріт" },
			{ "o.service_lever_upper", "верхній важіль" },
			{ "o.service_lever_lower", "нижній важіль" },

			// Texts.
			{ "t.bed", "Дерев'яне ліжко з гнилою соломою. Тут нічого корисного." },
			{ "t.wall", "На стіні подряпано: «Один оберт ліворуч. Два оберти праворуч. Тоді підняти.»" },
			{ "t.cup.moved", "Ви відсунули чашку. Під нею лежить зігнутий дріт." },
			{ "t.cup.done", "Порожня глиняна чашка." },
			{ "t.wire", "Ви взяли зігнутий дріт." },
			{ "t.latch.nowire", "Защіпка пошкоджена. Потрібно щось тонке, щоб її відімкнути." },
			{ "t.latch.open", "Замок відімкнено." },
			{ "t.latch.step", "Клац." },
			{ "t.latch.wrong", "Глухий стук. Почніть спочатку." },
			{ "t.ledger", "У книзі позначено порядок записів: СОНЦЕ — ПШЕНИЦЯ — МІСЯЦЬ." },
			{ "t.shelf", "Мішки з борошном, мотузки, ящики. Нічого потрібного." },
			{ "t.cabinet.wrong", "Шафа не піддається." },
			{ "t.cabinet.open", "Шафа відчинилася." },
			{ "t.pouch", "Ви взяли свою торбинку. У ній половинка ключа і записка." },
			{ "t.note", "«Ключ від службових воріт розділено надвоє проти крадіїв. Друга половинка лежить на верстаку біля воріт.»" },
			{ "t.half.b", "Ви взяли другу половинку ключа." },
			{ "t.combined", "Половинки з'єдналися в один ключ." },
			{ "t.key.in", "Ключ повернувся в замку. Механізм розблоковано." },
			{ "t.key.missing", "Тут потрібен цілий ключ." },
			{ "t.plaque", "На табличці: «Підніми верхній засув перед тим, як опустити нижній.»" },
			{ "t.lever.blocked", "Важіль не рухається: механізм заблоковано." },
			{ "t.lever.upper", "Верхній засув піднято." },
			{ "t.lever.wrong", "Важелі повернулися на місце. Згадайте табличку." },
			{ "t.lever.open", "Ворота відчиняються!" },
			{ "t.lever.done", "Цей засув уже піднято." },
			{ "t.hide", "Ви ховаєтеся за бочками." },
			{ "t.saved", "Збережено." },

			// Items.
			{ "i.wire", "Зігнутий дріт" },
			{ "i.gate_key_half_a", "Половинка ключа (A)" },
			{ "i.gate_key_half_b", "Половинка ключа (B)" },
			{ "i.gate_key_complete", "Ключ від службових воріт" },
			{ "i.wire.d", "Тонкий дріт. Підійде до защіпки." },
			{ "i.gate_key_half_a.d", "Половина залізного ключа з торбинки." },
			{ "i.gate_key_half_b.d", "Друга половина залізного ключа з верстака." },
			{ "i.gate_key_complete.d", "Цілий ключ від службових воріт." },

			// Symbols.
			{ "s.Sun", "СОНЦЕ" },
			{ "s.Wheat", "ПШЕНИЦЯ" },
			{ "s.Moon", "МІСЯЦЬ" },
			{ "s.Tree", "ДЕРЕВО" },

			// Latch buttons.
			{ "latch.title", "Защіпка" },
			{ "latch.left", "Ліворуч" },
			{ "latch.right", "Праворуч" },
			{ "latch.lift", "Підняти" },
			{ "cabinet.title", "Шафа з записами" },

			// Clues (journal).
			{ "clue_wall", "Напис на стіні: ліворуч, праворуч, праворуч, підняти." },
			{ "clue_ledger", "Книга: СОНЦЕ — ПШЕНИЦЯ — МІСЯЦЬ." },
			{ "clue_note", "Записка: ключ від воріт з двох половинок; друга на верстаку." },
			{ "clue_plaque", "Табличка: верхній засув перед нижнім." },

			// Hints.
			{ "hint.p1.1", "Прочитай напис на стіні біля чашки." },
			{ "hint.p1.2", "Ліворуч, праворуч, праворуч, підняти." },
			{ "hint.p2.1", "Облікова книга показує порядок для шафи." },
			{ "hint.p2.2", "Сонце, Пшениця, Місяць." },
			{ "hint.p3.1", "Табличка підкаже, який важіль першим." },
			{ "hint.p3.2", "Спершу верхній важіль, потім нижній." },
			{ "hint.none", "Підказок більше немає." },
		};

		/// <summary>The text for a key. A missing key shows the key itself, so a gap is visible rather than silent.</summary>
		public static string T(string key) => Table.TryGetValue(key, out var text) ? text : key;

		public static string T(string key, params object[] args) => string.Format(T(key), args);

		public static bool Has(string key) => Table.ContainsKey(key);

		public static IEnumerable<string> Keys => Table.Keys;
	}
}
