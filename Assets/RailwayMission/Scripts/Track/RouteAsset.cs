using UnityEngine;

namespace RailwayMission.Track
{
    /// <summary>路線資產，可在 Inspector 中編輯控制點、車站、地標與速限。Create → Railway → Route。</summary>
    [CreateAssetMenu(menuName = "Railway/Route", fileName = "RouteAsset")]
    public class RouteAsset : ScriptableObject
    {
        public RouteDefinition route = new RouteDefinition();
    }
}
