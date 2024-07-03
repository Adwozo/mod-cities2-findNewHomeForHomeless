using HarmonyLib;
using Game.Simulation;
using Unity.Entities;
using Unity.Collections;
using Game.Buildings;
using Unity.Mathematics;
using Game.Net;
using Game.Objects;
using Game.Citizens;
using Game.City;
using Game.Prefabs;
using Colossal.Logging;

namespace Homeless_Shelter_Pathfinding
{
    [HarmonyPatch(typeof(PropertyUtils))]
    [HarmonyPatch("GetPropertyScore")]
    public static class GetPropertyScorePatch
    {
        private static readonly ILog log = LogManager.GetLogger($"{nameof(Homeless_Shelter_Pathfinding)}.Combined.{nameof(Homeless_Shelter_Pathfinding)}").SetShowsErrorsInUI(false);

        [HarmonyPrefix]
        public static bool Prefix(
            ref float __result,
            Entity property,
            Entity household,
            DynamicBuffer<HouseholdCitizen> citizenBuffer,
            ref ComponentLookup<PrefabRef> prefabRefs,
            ref ComponentLookup<BuildingPropertyData> buildingProperties,
            ref ComponentLookup<Building> buildings,
            ref ComponentLookup<BuildingData> buildingDatas,
            ref ComponentLookup<Household> households,
            ref ComponentLookup<Citizen> citizens,
            ref ComponentLookup<Game.Citizens.Student> students,
            ref ComponentLookup<Worker> workers,
            ref ComponentLookup<SpawnableBuildingData> spawnableDatas,
            ref ComponentLookup<CrimeProducer> crimes,
            ref BufferLookup<Game.Net.ServiceCoverage> serviceCoverages,
            ref ComponentLookup<Locked> locked,
            ref ComponentLookup<ElectricityConsumer> electricityConsumers,
            ref ComponentLookup<WaterConsumer> waterConsumers,
            ref ComponentLookup<GarbageProducer> garbageProducers,
            ref ComponentLookup<MailProducer> mailProducers,
            ref ComponentLookup<Transform> transforms,
            ref ComponentLookup<Abandoned> abandoneds,
            ref ComponentLookup<Game.Buildings.Park> parks,
            ref BufferLookup<ResourceAvailability> availabilities,
            NativeArray<int> taxRates,
            NativeArray<GroundPollution> pollutionMap,
            NativeArray<AirPollution> airPollutionMap,
            NativeArray<NoisePollution> noiseMap,
            CellMapData<TelecomCoverage> telecomCoverages,
            DynamicBuffer<CityModifier> cityModifiers,
            Entity healthcareService,
            Entity entertainmentService,
            Entity educationService,
            Entity telecomService,
            Entity garbageService,
            Entity policeService,
            CitizenHappinessParameterData citizenHappinessParameterData,
            GarbageParameterData garbageParameterData)
        {
            if (!buildings.HasComponent(property))
            {
                __result = float.NegativeInfinity;
                return false;
            }

            Household householdData = households[household];

            bool flag = (householdData.m_Flags & HouseholdFlags.MovedIn) != 0;
            if ((parks.HasComponent(property) || abandoneds.HasComponent(property)) && !flag)
            {
                __result = float.NegativeInfinity;
                return false;
            }

            Building buildingData = buildings[property];
            Entity prefab = prefabRefs[property].m_Prefab;
            var genericApartmentQuality = PropertyUtils.GetGenericApartmentQuality(property, prefab, ref buildingData, ref buildingProperties, ref buildingDatas, ref spawnableDatas, ref crimes, ref serviceCoverages, ref locked, ref electricityConsumers, ref waterConsumers, ref garbageProducers, ref mailProducers, ref transforms, ref abandoneds, pollutionMap, airPollutionMap, noiseMap, telecomCoverages, cityModifiers, healthcareService, entertainmentService, educationService, telecomService, garbageService, policeService, citizenHappinessParameterData, garbageParameterData);

            int householdSize = citizenBuffer.Length;
            float totalCommuteTime = 0f;
            int workingOrStudyingMembers = 0;
            int adultMembers = 0;
            int totalHappiness = 0;
            int childMembers = 0;
            int totalTaxBonus = 0;
            int householdWealth = householdData.m_Resources;

            UnityEngine.Debug.LogWarning($"Household size: {householdSize}, Household wealth: {householdWealth}");

            for (int i = 0; i < householdSize; i++)
            {
                Entity citizenEntity = citizenBuffer[i].m_Citizen;
                Citizen citizen = citizens[citizenEntity];
                totalHappiness += citizen.Happiness;

                if (citizen.GetAge() == CitizenAge.Child)
                {
                    childMembers++;
                }
                else
                {
                    adultMembers++;
                    totalTaxBonus += CitizenHappinessSystem.GetTaxBonuses(citizen.GetEducationLevel(), taxRates, in citizenHappinessParameterData).y;
                }

                if (students.HasComponent(citizenEntity))
                {
                    workingOrStudyingMembers++;
                    var student = students[citizenEntity];
                    if (student.m_School != property)
                    {
                        totalCommuteTime += student.m_LastCommuteTime;
                    }
                }
                else if (workers.HasComponent(citizenEntity))
                {
                    workingOrStudyingMembers++;
                    var worker = workers[citizenEntity];
                    if (worker.m_Workplace != property)
                    {
                        totalCommuteTime += worker.m_LastCommuteTime;
                    }
                }
            }

            UnityEngine.Debug.LogWarning($"Adults: {adultMembers}, Children: {childMembers}, Working/Studying: {workingOrStudyingMembers}");
            UnityEngine.Debug.LogWarning($"Total Happiness: {totalHappiness}, Total Tax Bonus: {totalTaxBonus}, Total Commute Time: {totalCommuteTime}");

            float averageCommuteTime = (workingOrStudyingMembers > 0) ? totalCommuteTime / workingOrStudyingMembers : 0f;
            float averageHappiness = (householdSize > 0) ? (float)totalHappiness / householdSize : 0f;
            float averageTaxBonus = (adultMembers > 0) ? (float)totalTaxBonus / adultMembers : 0f;

            UnityEngine.Debug.LogWarning($"Average Commute Time: {averageCommuteTime}, Average Happiness: {averageHappiness}, Average Tax Bonus: {averageTaxBonus}");

            float serviceAvailability = PropertyUtils.GetServiceAvailability(buildingData.m_RoadEdge, buildingData.m_CurvePosition, availabilities);
            float cachedApartmentQuality = PropertyUtils.GetCachedApartmentQuality(householdSize, childMembers, totalHappiness, genericApartmentQuality);

            UnityEngine.Debug.LogWarning($"Service Availability: {serviceAvailability}, Cached Apartment Quality: {cachedApartmentQuality}");

            // Calculate a wealth-based penalty for parks
            float wealthFactor = math.log10(math.max(1f, householdWealth)) / 5f;
            float parkPenalty = 0f;

            if (parks.HasComponent(property))
            {
                parkPenalty = 1000f * wealthFactor;
                UnityEngine.Debug.LogWarning($"Property is a park. Wealth Factor: {wealthFactor}, Park Penalty: {parkPenalty}");
            }
            else
            {
                UnityEngine.Debug.LogWarning("Property is not a park.");
            }

            // Calculate the final score
            float baseScore = serviceAvailability + cachedApartmentQuality + (2f * averageTaxBonus) - averageCommuteTime;
            __result = baseScore - parkPenalty;

            UnityEngine.Debug.LogWarning($"Base Score: {baseScore}, Final Score: {__result}");

            // Ensure the result doesn't go below negative infinity
            if (__result < float.NegativeInfinity)
            {
                __result = float.NegativeInfinity;
                UnityEngine.Debug.LogWarning("Score set to negative infinity due to being too low.");
            }

            return false;
        }
    }
}