using System.Collections.Generic;

namespace DvSqlGenWeb.Models
{
    public class DVSchema
    {
        public Dictionary<string, Section> sections { get; set; } = new(); // ключ=SectionId(Guid), значение=описание секции
    }

    public class Section
    {
        public string alias { get; set; } = "";
        public string alias_ru { get; set; } = "";// алиас секции (например, MainInfo)
        public string card_type_id { get; set; } = "";      // Guid типа карточки
        public string card_type_alias { get; set; } = "";   // алиас типа карточки (напр., CardDocument)
        public List<Field> fields { get; set; } = new();    // список полей секции
    }

    public class Field
    {
        public string field_id { get; set; } = "";          // Guid поля
        public string alias { get; set; } = "";             // алиас поля (Name, State, Author и т.п.)
        public string section_alias { get; set; } = "";     // алиас секции, если присутствует
        public int type { get; set; }                       // тип (твои коды: 0-int,1-bit,2-datetime,...)
        public int max { get; set; }                        // длина для строк
        public bool is_dynamic { get; set; }                // флаги из твоей схемы
        public bool is_extended { get; set; }
        public bool is_new { get; set; }
        public Reference? references { get; set; }           // ссылка на др. секцию/справочник

        public List<string> synonyms { get; set; } = new();
    }

    public class Reference
    {
        public string section_type_id { get; set; } = "";   // Guid секции-назначения
        public string section_alias { get; set; } = "";     // алиас секции-назначения
        public string card_type_id { get; set; } = "";      // Guid карточки-назначения
        public string card_type_alias { get; set; } = "";   // алиас карточки-назначения
        public string target { get; set; } = "";            // человекочитаемая цель, если есть
    }
}
