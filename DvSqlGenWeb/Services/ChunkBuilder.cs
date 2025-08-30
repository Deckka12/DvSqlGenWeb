using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DvSqlGenWeb.Models;

namespace DvSqlGenWeb.Services
{
    public static class ChunkBuilder
    {
        //public static List<(string CardType, string SectionAlias, string SectionId, string Content, string field)> BuildChunks(DVSchema schema)
        //{
        //    var result = new List<(string, string, string, string, string)>();
        //    if (schema == null || schema.sections == null || schema.sections.Count == 0) 
        //        return result;

        //    var groupedByCard = schema.sections
        //        .Where(kv => kv.Value != null)
        //        .GroupBy(kv => SafeStr(kv.Value.card_type_alias, "Неизвестный тип"), StringComparer.OrdinalIgnoreCase)
        //        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        //    foreach (var card in groupedByCard)
        //    {
        //        var first = card.First().Value;
        //        var cardTypeAlias = card.Key;
        //        var cardTypeId = SafeStr(first.card_type_id, "00000000-0000-0000-0000-000000000000");

        //        foreach (var kv in card.OrderBy(kv => SafeStr(kv.Value.alias, ""), StringComparer.OrdinalIgnoreCase))
        //        {
        //            var sectionId = kv.Key;
        //            var sec = kv.Value;
        //            var sectionAlias = SafeStr(sec.alias, "Неизвестный тип");

        //            var fields = CollectSectionColumns(sec);
        //            if (fields.Count == 0) continue;

        //            var ordered = OrderColumns(fields);

        //            var sb = new StringBuilder();
        //            sb.AppendLine("TABLE: dvtable_{" + sectionId + "}");
        //            sb.AppendLine("CARD_TYPE: " + cardTypeAlias);
        //            sb.AppendLine("CARD_TYPE_ID: " + cardTypeId);
        //            sb.AppendLine("SECTION: " + sectionAlias);
        //            sb.AppendLine("SECTION_ID: " + sectionId);
        //            sb.AppendLine("COLUMNS:");
        //            foreach (var f in ordered)
        //            {
        //                sb.AppendLine(FormatColumnLine(f));
        //                var content = sb.ToString().TrimEnd();
        //                result.Add((cardTypeAlias, sectionAlias, sectionId, content, f.alias));

        //            }

        //        }
        //    }

        //    return result;
        //}

        //public static List<(string CardType, string SectionAlias, string SectionId, string Content, string FieldAlias)> BuildChunks(DVSchema schema)
        //{
        //    var result = new List<(string, string, string, string, string)>();
        //    if (schema?.sections == null || schema.sections.Count == 0)
        //        return result;

        //    var groupedByCard = schema.sections
        //        .Where(kv => kv.Value != null)
        //        .GroupBy(kv => SafeStr(kv.Value.card_type_alias, "Неизвестный тип"), StringComparer.OrdinalIgnoreCase)
        //        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        //    foreach (var card in groupedByCard)
        //    {
        //        var first = card.First().Value;
        //        var cardTypeAlias = card.Key;
        //        var cardTypeId = SafeStr(first.card_type_id, "00000000-0000-0000-0000-000000000000");

        //        foreach (var kv in card.OrderBy(kv => SafeStr(kv.Value.alias, ""), StringComparer.OrdinalIgnoreCase))
        //        {
        //            var sectionId = kv.Key;
        //            var sec = kv.Value;
        //            var sectionAlias = SafeStr(sec.alias, "Неизвестный тип");

        //            var fields = CollectSectionColumns(sec);
        //            if (fields.Count == 0) continue;

        //            var ordered = OrderColumns(fields);

        //            foreach (var f in ordered)
        //            {
        //                var sb = new StringBuilder();
        //                sb.AppendLine($"TABLE: dvtable_{{{sectionId}}}");
        //                //sb.AppendLine($"CARD_TYPE: {cardTypeAlias}");
        //                //sb.AppendLine($"CARD_TYPE_ID: {cardTypeId}");
        //                sb.AppendLine($"SECTION: {sectionAlias}");
        //                //sb.AppendLine($"SECTION_ID: {sectionId}");
        //                sb.AppendLine("FIELD:");
        //                sb.AppendLine(FormatColumnLine(f));

        //                var content = sb.ToString().TrimEnd();

        //                result.Add((cardTypeAlias, sectionAlias, sectionId, content, f.alias));
        //            }
        //        }
        //    }

        //    return result;
        //}

        public static List<(string CardType, string SectionAlias, string SectionId, string Content, string FieldAlias)> BuildChunks(DVSchema schema)
        {
            var result = new List<(string, string, string, string, string)>();
            if (schema?.sections == null || schema.sections.Count == 0)
                return result;

            var groupedByCard = schema.sections
                .Where(kv => kv.Value != null)
                .GroupBy(kv => SafeStr(kv.Value.card_type_alias, "Неизвестный тип"), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var card in groupedByCard)
            {
                var first = card.First().Value;
                var cardTypeAlias = card.Key;
                var cardTypeId = SafeStr(first.card_type_id, "00000000-0000-0000-0000-000000000000");

                foreach (var kv in card.OrderBy(kv => SafeStr(kv.Value.alias, ""), StringComparer.OrdinalIgnoreCase))
                {
                    var sectionId = kv.Key;
                    var sec = kv.Value;
                    var sectionAlias = SafeStr(sec.alias, "Неизвестный тип");

                    var fields = CollectSectionColumns(sec);
                    if (fields.Count == 0) continue;

                    var ordered = OrderColumns(fields);

                    foreach (var f in ordered)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"TABLE: dvtable_{{{sectionId}}}");
                        sb.AppendLine($"SECTION: {sectionAlias}");
                        sb.AppendLine("FIELD:");
                        sb.AppendLine(FormatColumnLine(f));

                        if (f.synonyms != null && f.synonyms.Count > 0)
                        {
                            sb.AppendLine("SYNONYMS: " + string.Join(", ", f.synonyms));
                        }

                        var content = sb.ToString().TrimEnd();

                        result.Add((cardTypeAlias, sectionAlias, sectionId, content, f.alias));
                    }
                }
            }

            return result;
        }



        private static string SafeStr(string s, string fallback) => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();

        private static List<Field> CollectSectionColumns(Section sec)
        {
            var list = new List<Field> { new Field { alias = "InstanceID", type = 7, max = 0 } };
            if (sec?.fields != null)
                foreach (var f in sec.fields)
                    if (f != null && !string.IsNullOrWhiteSpace(f.alias) &&
                        !list.Any(x => x.alias.Equals(f.alias, StringComparison.OrdinalIgnoreCase)))
                        list.Add(f);
            return list;
        }

        private static IEnumerable<Field> OrderColumns(List<Field> cols)
        {
            var keyOrder = new[] { "InstanceID", "State", "Name", "RegNumber", "Created", "CreatedBy" };
            var head = cols.Where(c => keyOrder.Any(k => k.Equals(c.alias, StringComparison.OrdinalIgnoreCase)))
                           .OrderBy(c => Array.IndexOf(keyOrder, keyOrder.First(k => k.Equals(c.alias, StringComparison.OrdinalIgnoreCase))));
            var tail = cols.Where(c => !keyOrder.Any(k => k.Equals(c.alias, StringComparison.OrdinalIgnoreCase)))
                           .OrderBy(c => c.alias, StringComparer.OrdinalIgnoreCase);
            return head.Concat(tail);
        }

        private static string FormatType(string alias, int type, int max) =>
            type switch
            {
                0 => "INT",
                1 => "BIT",
                2 => "DATETIME",
                5 => "ENUM",
                7 => "GUID",
                9 => "NUMERIC",
                10 or 16 => max > 0 ? $"NVARCHAR({max})" : "NVARCHAR(MAX)",
                12 or 20 => "DECIMAL(38,10)",
                13 => "REF",
                14 => "REFCard",
                15 => "XML",
                _ => (!string.IsNullOrEmpty(alias) && (alias.Equals("InstanceID", StringComparison.OrdinalIgnoreCase) ||
                                                       alias.Equals("State", StringComparison.OrdinalIgnoreCase) ||
                                                       alias.EndsWith("Id", StringComparison.OrdinalIgnoreCase)))
                        ? "GUID"
                        : (max > 0 ? $"NVARCHAR({max})" : "NVARCHAR(MAX)")
            };

        private static string FormatColumnLine(Field f)
        {
            var typed = FormatType(f.alias, f.type, f.max);
            if ((f.type == 13 ) && f.references != null)
            {
                var target = BuildReferenceTarget(f.references);
                if (!string.IsNullOrWhiteSpace(target))
                {
                    var targetTable = $"dvtable_{{{f.references.section_type_id}}}";
                    return $"- {f.alias} {typed} \u2192 {target} (JOIN ON ThisTable.{f.alias} = [{targetTable}].RowID)";
                }
            }

            if ((f.type == 14) && f.references != null)
            {
                var target = BuildReferenceTarget(f.references);
                if (!string.IsNullOrWhiteSpace(target))
                {
                    var targetTable = $"dvtable_{{{f.references.section_type_id}}}";
                    return $"- {f.alias} {typed} \u2192 {target} (JOIN ON ThisTable.{f.alias} = [{targetTable}].InstanceID)";
                }
            }
            //f.type == 14
            return "- " + f.alias + " " + typed;
        }

        private static string BuildReferenceTarget(Reference r)
        {
            if (r == null) return null;
            if (!string.IsNullOrWhiteSpace(r.target)) return r.target.Trim();
            var card = (r.card_type_alias ?? "").Trim();
            var sec = (r.section_alias ?? "").Trim();
            if (!string.IsNullOrEmpty(card) && !string.IsNullOrEmpty(sec)) return card + "." + sec;
            if (!string.IsNullOrEmpty(sec)) return sec;
            return null;
        }
    }
}
