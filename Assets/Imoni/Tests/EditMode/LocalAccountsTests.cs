using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class LocalAccountsTests
    {
        private static JObject Config()
        {
#if UNITY_EDITOR
            return JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json")));
#else
            return JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json")));
#endif
        }

        private static ProfileData Member(JObject config, string name, int marker)
        {
            JToken defaults = config["outGame"]["profile"];
            string first = config["titles"]["firstParts"][1].Value<string>();
            string second = config["titles"]["secondParts"][1].Value<string>();
            string material = config["materials"][0]["id"].Value<string>();
            string quality = config["qualities"][0]["id"].Value<string>();
            int clearRound = config["round"]["roundsToClear"].Value<int>();
            BigInteger exactScore = BigInteger.Pow(10, 60) + marker;
            return ProfilePersistence.Normalize(config, new ProfileData
            {
                Name = name, FirstTitle = first, SecondTitle = second,
                BandColor = defaults["bandColors"][1].Value<string>(), OutfitColor = defaults["outfitColors"][1].Value<string>(),
                TutorialCompleted = true, SoundEnabled = false, SoundVolume = 0.5, BgmVolume = 0.2, EffectsVolume = 0.8,
                OwnedFirstTitles = new[] { first }, OwnedSecondTitles = new[] { second },
                OwnedBadges = new[] { config["progression"]["badgeRules"][0]["id"].Value<string>() },
                AppliedRoundKeys = new[] { "challenge-" + marker + ":" + clearRound },
                Progression = new ProgressionStats
                {
                    BestRound = clearRound + marker, SuccessfulRound = clearRound, BestStreak = marker,
                    NormalClears = marker, BestScore = exactScore,
                    GatheredByMaterial = new Dictionary<string, BigInteger> { [material] = exactScore }, GatheredQualities = new[] { quality }
                },
                Rankings = new[]
                {
                    new LocalRankingRecord
                    {
                        ChallengeId = "challenge-" + marker, TeamName = "テスト隊" + marker, TeamSize = config["team"]["size"].Value<int>(),
                        Mode = RankingMode.Normal, SeedDivision = RankingSeed.Common, Seed = config["outGame"]["lobby"]["commonSeed"].Value<int>(),
                        RoundNumber = clearRound, Score = exactScore, RecordedAtUtc = new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero).AddMinutes(marker)
                    }
                }
            });
        }

        private static string Snapshot(JObject config, ProfileData profile) => ProfilePersistence.Save(config, profile);

        // Mutate both the outer object and every nested mutable kind retained in a profile.
        private static void Mutate(JObject config, ProfileData profile)
        {
            profile.Name = "外部の変更"; profile.TutorialCompleted = !profile.TutorialCompleted;
            profile.SoundEnabled = !profile.SoundEnabled; profile.BgmVolume = 0.9; profile.EffectsVolume = 0.1;
            profile.OwnedFirstTitles[0] = config["titles"]["firstParts"].Last.Value<string>();
            profile.OwnedSecondTitles[0] = config["titles"]["secondParts"].Last.Value<string>();
            if (profile.OwnedBadges.Length != 0) profile.OwnedBadges[0] = config["progression"]["badgeRules"].Last["id"].Value<string>();
            if (profile.AppliedRoundKeys.Length != 0) profile.AppliedRoundKeys[0] = "changed:1";
            profile.Progression.BestRound++;
            profile.Progression.GatheredByMaterial[config["materials"][0]["id"].Value<string>()] = 7;
            if (profile.Progression.GatheredQualities.Length != 0) profile.Progression.GatheredQualities[0] = config["qualities"].Last["id"].Value<string>();
            if (profile.Rankings.Length != 0) { profile.Rankings[0].TeamName = "変更された隊"; profile.Rankings[0].Score = 7; }
        }

        [Test]
        public void LegacySingleProfileMigratesTitlesVolumesAndAllPermanentRecords()
        {
            JObject config = Config(); JObject legacy = JObject.Parse(Snapshot(config, Member(config, "旧会員", 1)));
            legacy.Remove(nameof(ProfileData.OwnedFirstTitles)); legacy.Remove(nameof(ProfileData.OwnedSecondTitles));
            legacy.Remove(nameof(ProfileData.BgmVolume)); legacy.Remove(nameof(ProfileData.EffectsVolume));
            string json = legacy.ToString(); ProfileData expected = ProfilePersistence.Load(config, json);
            LocalAccounts accounts = LocalAccounts.Load(config, json);
            ProfileData profile = accounts.Get(config, accounts.ActiveId);
            Assert.That(accounts.Ids, Is.EqualTo(new[] { "local-default" }));
            Assert.That(Snapshot(config, profile), Is.EqualTo(Snapshot(config, expected)));
            Assert.That(profile.OwnedFirstTitles, Does.Contain(legacy["FirstTitle"].Value<string>()));
            Assert.That(profile.OwnedSecondTitles, Does.Contain(legacy["SecondTitle"].Value<string>()));
            Assert.That(profile.BgmVolume, Is.EqualTo(profile.SoundVolume));
            Assert.That(profile.EffectsVolume, Is.EqualTo(profile.SoundVolume));
            Assert.That(profile.Rankings.Single().Score, Is.EqualTo(Member(config, "旧会員", 1).Rankings.Single().Score));
            string upgraded = accounts.Save(config, profile);
            Assert.That(JObject.Parse(upgraded)["ActiveAccountId"].Value<string>(), Is.EqualTo(accounts.ActiveId));
            Assert.That(Snapshot(config, ProfilePersistence.Load(config, upgraded)), Is.EqualTo(Snapshot(config, expected)));
        }

        [Test]
        public void ThreeProfilesAndActiveSelectionSurviveRepeatedRestarts()
        {
            JObject config = Config(); ProfileData first = Member(config, "会員甲", 1), second = Member(config, "会員乙", 2), third = Member(config, "会員丙", 3);
            LocalAccounts accounts = LocalAccounts.Load(config, Snapshot(config, first)); string firstId = accounts.ActiveId;
            accounts.Create(config, "second", first, second.Name);
            accounts.Create(config, "third", second, third.Name);
            string json = accounts.Save(config, third);
            for (int restart = 0; restart < 2; restart++)
            {
                accounts = LocalAccounts.Load(config, json);
                Assert.That(accounts.ActiveId, Is.EqualTo("third"));
                Assert.That(accounts.Ids, Is.EquivalentTo(new[] { firstId, "second", "third" }));
                Assert.That(Snapshot(config, accounts.Get(config, firstId)), Is.EqualTo(Snapshot(config, first)));
                Assert.That(Snapshot(config, accounts.Get(config, "second")), Is.EqualTo(Snapshot(config, second)));
                Assert.That(Snapshot(config, accounts.Get(config, "third")), Is.EqualTo(Snapshot(config, third)));
                Assert.That(Snapshot(config, ProfilePersistence.Load(config, json)), Is.EqualTo(Snapshot(config, third)), "The active top-level profile remains backward readable.");
                json = accounts.Save(config, accounts.Get(config, accounts.ActiveId));
            }
        }

        [Test]
        public void SwitchingSavesOutgoingEditsAndRestoresEachMembersTitlesRankingsAndProgress()
        {
            JObject config = Config(); ProfileData first = Member(config, "会員甲", 1), second = Member(config, "会員乙", 2);
            LocalAccounts accounts = LocalAccounts.Load(config, Snapshot(config, first)); string firstId = accounts.ActiveId;
            first.Name = "編集後の甲"; first.Progression.BestScore += 11;
            accounts.Create(config, "second", first, second.Name);
            second.Name = "編集後の乙"; second.Rankings[0].Score += 22;
            ProfileData returnedFirst = accounts.Switch(config, firstId, second);
            Assert.That(accounts.ActiveId, Is.EqualTo(firstId));
            Assert.That(Snapshot(config, returnedFirst), Is.EqualTo(Snapshot(config, first)));
            ProfileData returnedSecond = accounts.Switch(config, "second", returnedFirst);
            Assert.That(Snapshot(config, returnedSecond), Is.EqualTo(Snapshot(config, second)));
            Assert.That(Snapshot(config, accounts.Get(config, firstId)), Is.EqualTo(Snapshot(config, first)));
        }

        [TestCase(null)]
        [TestCase("   ")]
        [TestCase("  新しい会員  ")]
        public void NewProfileUsesConfiguredDefaultsAndOnlyInheritsSoundPreferences(string name)
        {
            JObject config = Config(); config["outGame"]["profile"]["defaultName"] = "既定の新人";
            ProfileData old = Member(config, "実績あり", 2);
            LocalAccounts accounts = LocalAccounts.Load(config, Snapshot(config, old)); string oldId = accounts.ActiveId;
            ProfileData created = accounts.Create(config, "new", old, name);
            ProfileData expected = ProfilePersistence.Normalize(config, new ProfileData
            {
                Name = name, SoundEnabled = old.SoundEnabled, SoundVolume = old.SoundVolume,
                BgmVolume = old.BgmVolume, EffectsVolume = old.EffectsVolume
            });
            Assert.That(Snapshot(config, created), Is.EqualTo(Snapshot(config, expected)));
            Assert.That(created.TutorialCompleted, Is.False); Assert.That(created.OwnedBadges, Is.Empty);
            Assert.That(created.Rankings, Is.Empty); Assert.That(created.AppliedRoundKeys, Is.Empty);
            Assert.That(created.Progression.GatheredByMaterial, Is.Empty);
            Assert.That(Snapshot(config, accounts.Get(config, oldId)), Is.EqualTo(Snapshot(config, old)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("missing")]
        public void UnknownGetOrSwitchDoesNotChangeSelectionOrStoreUnsavedOutgoingData(string unknownId)
        {
            JObject config = Config(); ProfileData initial = Member(config, "元の会員", 1);
            LocalAccounts accounts = LocalAccounts.Load(config, Snapshot(config, initial)); string activeId = accounts.ActiveId;
            ProfileData dirty = Member(config, "保存しない変更", 2); string before = accounts.Save(config, initial);
            Assert.Catch<ArgumentException>(() => accounts.Get(config, unknownId));
            Assert.Catch<ArgumentException>(() => accounts.Switch(config, unknownId, dirty));
            Assert.That(accounts.ActiveId, Is.EqualTo(activeId)); Assert.That(accounts.Ids, Is.EqualTo(new[] { activeId }));
            Assert.That(accounts.Save(config, accounts.Get(config, activeId)), Is.EqualTo(before));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("local-default")]
        public void InvalidOrDuplicateCreateDoesNotOverwriteAnySavedMember(string rejectedId)
        {
            JObject config = Config(); ProfileData initial = Member(config, "元の会員", 1);
            LocalAccounts accounts = LocalAccounts.Load(config, Snapshot(config, initial)); string before = accounts.Save(config, initial);
            Assert.Throws<ArgumentException>(() => accounts.Create(config, rejectedId, Member(config, "保存しない変更", 2), "新会員"));
            Assert.That(accounts.ActiveId, Is.EqualTo("local-default"));
            Assert.That(accounts.Save(config, accounts.Get(config, accounts.ActiveId)), Is.EqualTo(before));
        }

        [TestCase("update")]
        [TestCase("save")]
        [TestCase("switch")]
        [TestCase("create")]
        public void StoredProfilesDoNotRetainCallerOwnedObjectsArraysDictionariesOrRankingRows(string operation)
        {
            JObject config = Config(); ProfileData initial = Member(config, "初期会員", 1);
            LocalAccounts accounts = LocalAccounts.Load(config, Snapshot(config, initial)); string firstId = accounts.ActiveId;
            ProfileData other = accounts.Create(config, "other", initial, "別会員"); accounts.Switch(config, firstId, other);
            ProfileData callerOwned = Member(config, "保存された会員", 2); string expected = Snapshot(config, callerOwned);
            switch (operation)
            {
                case "update": accounts.UpdateActive(config, callerOwned); break;
                case "save": accounts.Save(config, callerOwned); break;
                case "switch": accounts.Switch(config, "other", callerOwned); break;
                case "create": accounts.Create(config, "new", callerOwned, "新会員"); break;
            }
            Mutate(config, callerOwned);
            Assert.That(Snapshot(config, accounts.Get(config, firstId)), Is.EqualTo(expected));
        }

        [TestCase("get")]
        [TestCase("switch")]
        [TestCase("create")]
        public void ReturnedProfilesCanBeEditedWithoutMutatingStoredAccounts(string operation)
        {
            JObject config = Config(); ProfileData initial = Member(config, "元の会員", 1);
            LocalAccounts accounts = LocalAccounts.Load(config, Snapshot(config, initial)); string firstId = accounts.ActiveId;
            ProfileData returned;
            if (operation == "create") returned = accounts.Create(config, "new", initial, "新会員");
            else if (operation == "switch")
            {
                ProfileData other = accounts.Create(config, "other", initial, "別会員"); returned = accounts.Switch(config, firstId, other);
            }
            else returned = accounts.Get(config, firstId);
            string targetId = accounts.ActiveId, expected = Snapshot(config, returned);
            Mutate(config, returned);
            Assert.That(Snapshot(config, accounts.Get(config, targetId)), Is.EqualTo(expected));
            if (operation == "create") Assert.That(Snapshot(config, accounts.Get(config, firstId)), Is.EqualTo(Snapshot(config, initial)));
        }

        [TestCase("active")]
        [TestCase("local-default")]
        public void ActiveTopLevelProfileWinsOverStaleNestedCopyWithoutReplacingOtherAccounts(string activeId)
        {
            JObject config = Config(); ProfileData current = Member(config, "最新会員", 2), other = Member(config, "別会員", 3);
            JObject root = JObject.Parse(Snapshot(config, current));
            root["ActiveAccountId"] = activeId;
            root["AccountProfiles"] = new JObject
            {
                [activeId] = JObject.Parse(Snapshot(config, Member(config, "古い会員", 1))),
                ["other"] = JObject.Parse(Snapshot(config, other))
            };
            LocalAccounts accounts = LocalAccounts.Load(config, root.ToString());
            Assert.That(accounts.ActiveId, Is.EqualTo(activeId));
            Assert.That(Snapshot(config, accounts.Get(config, activeId)), Is.EqualTo(Snapshot(config, current)));
            Assert.That(Snapshot(config, accounts.Get(config, "other")), Is.EqualTo(Snapshot(config, other)));
        }

        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("false")]
        [TestCase("42")]
        [TestCase("null")]
        public void InvalidActiveIdTypeRecoversTopLevelProfileAndKeepsOtherStoredMembers(string invalidIdJson)
        {
            JObject config = Config(); ProfileData current = Member(config, "回復する会員", 1), other = Member(config, "別会員", 2);
            JObject root = JObject.Parse(Snapshot(config, current)); root["ActiveAccountId"] = JToken.Parse(invalidIdJson);
            root["AccountProfiles"] = new JObject { ["other"] = JObject.Parse(Snapshot(config, other)) };
            LocalAccounts accounts = LocalAccounts.Load(config, root.ToString());
            Assert.That(accounts.ActiveId, Is.EqualTo("local-default"));
            Assert.That(Snapshot(config, accounts.Get(config, accounts.ActiveId)), Is.EqualTo(Snapshot(config, current)));
            Assert.That(Snapshot(config, accounts.Get(config, "other")), Is.EqualTo(Snapshot(config, other)));
        }

        [TestCase(null)]
        [TestCase("null")]
        [TestCase("\"\"")]
        [TestCase("\"   \"")]
        [TestCase("[]")]
        [TestCase("{}")]
        [TestCase("42")]
        [TestCase("false")]
        public void MissingOrDamagedActiveIdPreservesDefaultMemberAndRecoversTopLevelToNewPersistentId(string invalidIdJson)
        {
            JObject config = Config();
            ProfileData original = Member(config, "元の既定会員", 1), other = Member(config, "別会員", 2);
            ProfileData recovered = Member(config, "復旧する会員", 3), previousRecovery = Member(config, "過去の復旧会員", 4);
            const string previousRecoveryId = "local-recovered-00000000000000000000000000000000";
            JObject root = JObject.Parse(Snapshot(config, recovered));
            if (invalidIdJson != null) root["ActiveAccountId"] = JToken.Parse(invalidIdJson);
            root["AccountProfiles"] = new JObject
            {
                ["local-default"] = JObject.Parse(Snapshot(config, original)),
                ["other"] = JObject.Parse(Snapshot(config, other)),
                [previousRecoveryId] = JObject.Parse(Snapshot(config, previousRecovery))
            };
            LocalAccounts accounts = LocalAccounts.Load(config, root.ToString()); string recoveredId = accounts.ActiveId;
            Assert.That(recoveredId, Does.StartWith("local-recovered-"));
            Assert.That(recoveredId, Is.Not.EqualTo(previousRecoveryId));
            Assert.That(Guid.TryParseExact(recoveredId.Substring("local-recovered-".Length), "N", out _), Is.True);
            for (int restart = 0; restart < 2; restart++)
            {
                Assert.That(accounts.ActiveId, Is.EqualTo(recoveredId));
                Assert.That(accounts.Ids, Is.EquivalentTo(new[] { "local-default", "other", previousRecoveryId, recoveredId }));
                Assert.That(Snapshot(config, accounts.Get(config, "local-default")), Is.EqualTo(Snapshot(config, original)));
                Assert.That(Snapshot(config, accounts.Get(config, "other")), Is.EqualTo(Snapshot(config, other)));
                Assert.That(Snapshot(config, accounts.Get(config, previousRecoveryId)), Is.EqualTo(Snapshot(config, previousRecovery)));
                Assert.That(Snapshot(config, accounts.Get(config, recoveredId)), Is.EqualTo(Snapshot(config, recovered)));
                accounts = LocalAccounts.Load(config, accounts.Save(config, accounts.Get(config, recoveredId)));
            }
        }
    }
}
