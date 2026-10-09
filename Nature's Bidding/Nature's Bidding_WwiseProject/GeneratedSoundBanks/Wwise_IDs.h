/////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Audiokinetic Wwise generated include file. Do not edit.
//
/////////////////////////////////////////////////////////////////////////////////////////////////////

#ifndef __WWISE_IDS_H__
#define __WWISE_IDS_H__

#include <AK/SoundEngine/Common/AkTypes.h>

namespace AK
{
    namespace EVENTS
    {
        static const AkUniqueID PLAY_AMB_FOREST = 994146778U;
        static const AkUniqueID PLAY_AMB_LAVA = 3906149303U;
        static const AkUniqueID PLAY_GP_MANMASK_EXPLOSION = 2410750046U;
        static const AkUniqueID PLAY_GP_PLAYER_ATTACKSLASH = 3812350027U;
        static const AkUniqueID PLAY_GP_PLAYER_CLIFFFALL = 313685605U;
        static const AkUniqueID PLAY_GP_PLAYER_DEATH = 2900211770U;
        static const AkUniqueID PLAY_GP_PLAYER_DOUBLEJUMP = 3835938179U;
        static const AkUniqueID PLAY_GP_PLAYER_PARRYSUCCESS = 2538377831U;
        static const AkUniqueID PLAY_GP_PLAYER_SHIELD = 3484761007U;
        static const AkUniqueID PLAY_GP_PLAYER_SHIELD_FAIL = 2926918818U;
        static const AkUniqueID PLAY_GP_PLAYER_TAKEHIT = 1472718208U;
        static const AkUniqueID PLAY_GP_PLAYER_WARP = 4114437350U;
        static const AkUniqueID PLAY_GP_ROCKSLIDE = 2373231564U;
        static const AkUniqueID PLAY_MX_SYSTEM = 3580595815U;
        static const AkUniqueID PLAY_UI_BID_ADJUST = 3980822666U;
        static const AkUniqueID PLAY_UI_BID_REJECT = 1191336738U;
        static const AkUniqueID PLAY_UI_BID_SUBMIT = 2289452967U;
        static const AkUniqueID PLAY_UI_CLICK = 1749424733U;
        static const AkUniqueID PLAY_UI_HOVER = 1339559671U;
        static const AkUniqueID PLAY_UI_SHOP_PURCHASE = 2035278795U;
        static const AkUniqueID PLAY_VO_AUCTIONEER = 773126247U;
        static const AkUniqueID PLAY_VO_PLAYER_JUMPGRUNT = 1992741306U;
        static const AkUniqueID STOP_AMB_FOREST = 3443576024U;
        static const AkUniqueID STOP_AMB_FOREST_01 = 1443530286U;
        static const AkUniqueID STOP_AMB_LAVA = 3566511473U;
        static const AkUniqueID STOP_GP_ROCKSLIDE = 758397350U;
        static const AkUniqueID STOP_MX_SYSTEM = 3534127541U;
    } // namespace EVENTS

    namespace STATES
    {
        namespace COMBAT_RESULT
        {
            static const AkUniqueID GROUP = 3737198187U;

            namespace STATE
            {
                static const AkUniqueID DEFEAT = 1593864692U;
                static const AkUniqueID NONE = 748895195U;
                static const AkUniqueID VICTORY = 2716678721U;
            } // namespace STATE
        } // namespace COMBAT_RESULT

        namespace GAME_PHASE
        {
            static const AkUniqueID GROUP = 3211383847U;

            namespace STATE
            {
                static const AkUniqueID BIDDING = 500842706U;
                static const AkUniqueID COMBAT = 2764240573U;
                static const AkUniqueID LOBBY = 290285391U;
                static const AkUniqueID MENU = 2607556080U;
                static const AkUniqueID NONE = 748895195U;
            } // namespace STATE
        } // namespace GAME_PHASE

        namespace MAP
        {
            static const AkUniqueID GROUP = 1048449605U;

            namespace STATE
            {
                static const AkUniqueID CLIFF = 1668395945U;
                static const AkUniqueID LAVA = 540301611U;
                static const AkUniqueID NONE = 748895195U;
            } // namespace STATE
        } // namespace MAP

        namespace PLAYERS
        {
            static const AkUniqueID GROUP = 2188949101U;

            namespace STATE
            {
                static const AkUniqueID FOUR = 2863728729U;
                static const AkUniqueID NONE = 748895195U;
                static const AkUniqueID THREE = 912956111U;
                static const AkUniqueID TWO = 678209053U;
            } // namespace STATE
        } // namespace PLAYERS

    } // namespace STATES

    namespace SWITCHES
    {
        namespace BID_DIRECTION
        {
            static const AkUniqueID GROUP = 4110079352U;

            namespace SWITCH
            {
                static const AkUniqueID DOWN = 2280510569U;
                static const AkUniqueID UP = 1551306158U;
            } // namespace SWITCH
        } // namespace BID_DIRECTION

        namespace IMPACT_TYPE
        {
            static const AkUniqueID GROUP = 2161705626U;

            namespace SWITCH
            {
            } // namespace SWITCH
        } // namespace IMPACT_TYPE

        namespace MASK_TYPE
        {
            static const AkUniqueID GROUP = 2391623980U;

            namespace SWITCH
            {
            } // namespace SWITCH
        } // namespace MASK_TYPE

        namespace PLAYER
        {
            static const AkUniqueID GROUP = 1069431850U;

            namespace SWITCH
            {
                static const AkUniqueID FOUR = 2863728729U;
                static const AkUniqueID ONE = 1064933119U;
                static const AkUniqueID THREE = 912956111U;
                static const AkUniqueID TWO = 678209053U;
            } // namespace SWITCH
        } // namespace PLAYER

        namespace SURFACE
        {
            static const AkUniqueID GROUP = 1834394558U;

            namespace SWITCH
            {
            } // namespace SWITCH
        } // namespace SURFACE

        namespace WEAPON_TYPE
        {
            static const AkUniqueID GROUP = 3942364280U;

            namespace SWITCH
            {
            } // namespace SWITCH
        } // namespace WEAPON_TYPE

    } // namespace SWITCHES

    namespace GAME_PARAMETERS
    {
        static const AkUniqueID AMBIENCE_VOLUME = 2901143258U;
        static const AkUniqueID COMBAT_PAN = 2369427653U;
        static const AkUniqueID MASTER_VOLUME = 4179668880U;
        static const AkUniqueID MUSIC_VOLUME = 1006694123U;
        static const AkUniqueID SFX_VOLUME = 1564184899U;
        static const AkUniqueID UI_VOLUME = 1719345792U;
    } // namespace GAME_PARAMETERS

    namespace BUSSES
    {
        static const AkUniqueID AMBIENCE = 85412153U;
        static const AkUniqueID MAIN_AUDIO_BUS = 2246998526U;
        static const AkUniqueID MUSIC = 3991942870U;
        static const AkUniqueID SFX = 393239870U;
        static const AkUniqueID UI = 1551306167U;
    } // namespace BUSSES

    namespace AUDIO_DEVICES
    {
        static const AkUniqueID NO_OUTPUT = 2317455096U;
        static const AkUniqueID SYSTEM = 3859886410U;
    } // namespace AUDIO_DEVICES

}// namespace AK

#endif // __WWISE_IDS_H__
