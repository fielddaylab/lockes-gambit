using BeauUtil;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Supply
{
    public static class SupplyConsts
    {

    }

    public static class SupplyScriptTriggers
    {
        public static readonly StringHash32 OnSupplySetupCompleted = "OnSupplySetupCompleted";
        public static readonly StringHash32 OnPlanetHovered = "OnPlanetHovered";
        public static readonly StringHash32 OnRouteCompleted = "OnRouteCompleted";
        public static readonly StringHash32 OnRoutesConfirmed = "OnRoutesConfirmed";
        public static readonly StringHash32 OnRouteSegmentDeleted = "OnRouteSegmentDeleted";
        public static readonly StringHash32 OnRouteFullyRemoved = "OnRouteFullyRemoved";
        public static readonly StringHash32 OnShipSelected = "OnShipSelected";
        public static readonly StringHash32 OnNodeClicked = "OnNodeClicked";
    }
}
