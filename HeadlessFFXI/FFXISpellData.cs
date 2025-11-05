using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FFXISpellData
{
    /// <summary>
    /// Represents a spell's level requirement for a specific job
    /// </summary>
    public record SpellLevel(int JobId, int Level);

    /// <summary>
    /// Represents a single spell from the FFXI spells.ula data
    /// </summary>
    public record Spell
    {
        public uint Id { get; init; }
        public string EnglishName { get; init; } = string.Empty;
        public double CastTime { get; init; }
        public int Element { get; init; }
        public Dictionary<int, int> Levels { get; init; } = new();
        public int MpCost { get; init; }
        public double Range { get; init; }
        public double Recast { get; init; }
        public int RecastId { get; init; }
        public int Requirements { get; init; }
        public int Targets { get; init; }

        /// <summary>
        /// Gets the level requirement for a specific job ID
        /// </summary>
        public int? GetLevelForJob(int jobId)
        {
            return Levels.TryGetValue(jobId, out var level) ? level : null;
        }

        /// <summary>
        /// Gets all jobs that can use this spell
        /// </summary>
        public IEnumerable<SpellLevel> GetAllJobLevels()
        {
            return Levels.Select(kvp => new SpellLevel(kvp.Key, kvp.Value));
        }
    }

    /// <summary>
    /// Repository for managing spell data
    /// </summary>
    public class SpellRepository
    {
        private readonly Dictionary<uint, Spell> _spellsById = new();
        private readonly Dictionary<string, Spell> _spellsByName = new();

        public int Count => _spellsById.Count;

        /// <summary>
        /// Adds a spell to the repository
        /// </summary>
        public void AddSpell(Spell spell)
        {
            _spellsById[spell.Id] = spell;
            _spellsByName[spell.EnglishName.ToLowerInvariant()] = spell;
        }

        /// <summary>
        /// Gets a spell by its ID
        /// </summary>
        public Spell? GetById(uint id)
        {
            return _spellsById.TryGetValue(id, out var spell) ? spell : null;
        }

        /// <summary>
        /// Gets a spell by its English name (case-insensitive)
        /// </summary>
        public Spell? GetByName(string name)
        {
            return _spellsByName.TryGetValue(name.ToLowerInvariant(), out var spell) ? spell : null;
        }

        /// <summary>
        /// Gets all spells
        /// </summary>
        public IEnumerable<Spell> GetAll()
        {
            return _spellsById.Values;
        }

        /// <summary>
        /// Searches for spells by partial name match (case-insensitive)
        /// </summary>
        public IEnumerable<Spell> SearchByName(string partialName)
        {
            var lower = partialName.ToLowerInvariant();
            return _spellsByName
                .Where(kvp => kvp.Key.Contains(lower))
                .Select(kvp => kvp.Value);
        }

        /// <summary>
        /// Gets all spells available to a specific job at a given level
        /// </summary>
        public IEnumerable<Spell> GetSpellsForJob(int jobId, int? maxLevel = null)
        {
            return _spellsById.Values
                .Where(s => s.Levels.ContainsKey(jobId) &&
                           (!maxLevel.HasValue || s.Levels[jobId] <= maxLevel.Value));
        }
    }

    /// <summary>
    /// Parser for FFXI spells.lua file format
    /// </summary>
    public class SpellLuaParser
    {
        /// <summary>
        /// Parses a spells.lua file and returns a populated SpellRepository
        /// </summary>
        public static SpellRepository ParseFile(string filePath)
        {
            var content = File.ReadAllText(filePath);
            return ParseContent(content);
        }

        /// <summary>
        /// Parses spell data from lua content string
        /// </summary>
        public static SpellRepository ParseContent(string luaContent)
        {
            var repository = new SpellRepository();

            // Match all spell entries: [id] = {properties}
            var spellPattern = @"\[(\d+)\]\s*=\s*\{([^}]+)\}";
            var matches = Regex.Matches(luaContent, spellPattern);

            foreach (Match match in matches)
            {
                try
                {
                    var id = uint.Parse(match.Groups[1].Value);
                    var properties = match.Groups[2].Value;

                    var spell = ParseSpell(id, properties);
                    if (spell != null)
                    {
                        repository.AddSpell(spell);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing spell: {ex.Message}");
                }
            }

            return repository;
        }

        private static Spell? ParseSpell(uint id, string properties)
        {
            var spell = new Spell { Id = id };

            // Extract English name
            var enMatch = Regex.Match(properties, @"en=""([^""]+)""");
            if (!enMatch.Success) return null;

            var englishName = enMatch.Groups[1].Value;

            // Extract cast_time
            var castTime = ExtractDouble(properties, "cast_time");

            // Extract element
            var element = ExtractInt(properties, "element");

            // Extract levels dictionary
            var levels = ExtractLevels(properties);

            // Extract mp_cost
            var mpCost = ExtractInt(properties, "mp_cost");

            // Extract range
            var range = ExtractDouble(properties, "range");

            // Extract recast
            var recast = ExtractDouble(properties, "recast");

            // Extract recast_id
            var recastId = ExtractInt(properties, "recast_id");

            // Extract requirements
            var requirements = ExtractInt(properties, "requirements");

            // Extract targets
            var targets = ExtractInt(properties, "targets");

            return spell with
            {
                EnglishName = englishName,
                CastTime = castTime,
                Element = element,
                Levels = levels,
                MpCost = mpCost,
                Range = range,
                Recast = recast,
                RecastId = recastId,
                Requirements = requirements,
                Targets = targets
            };
        }

        private static Dictionary<int, int> ExtractLevels(string properties)
        {
            Console.WriteLine(properties);
            var levels = new Dictionary<int, int>();
            var levelsMatch = Regex.Match(properties, @"levels=\{(.*?)\}");

            if (levelsMatch.Success)
            {
                Console.WriteLine("levels");
                var levelsContent = levelsMatch.Groups[1].Value;
                var levelMatches = Regex.Matches(properties, @"\[(\d+)\]\s*=\s*(\d+)");

                foreach (Match levelMatch in levelMatches)
                {
                    var jobId = int.Parse(levelMatch.Groups[1].Value);
                    var level = int.Parse(levelMatch.Groups[2].Value);
                    Console.WriteLine("{0:G} {1:G}",jobId, level);
                    levels[jobId] = level;
                }
            }

            return levels;
        }

        private static int ExtractInt(string properties, string fieldName)
        {
            var match = Regex.Match(properties, $@"{fieldName}=(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private static double ExtractDouble(string properties, string fieldName)
        {
            var match = Regex.Match(properties, $@"{fieldName}=([\d.]+)");
            return match.Success ? double.Parse(match.Groups[1].Value) : 0.0;
        }
    }
}