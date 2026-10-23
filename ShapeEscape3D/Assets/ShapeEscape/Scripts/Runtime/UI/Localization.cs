using System.Collections.Generic;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Minimal string-table localisation (English, Spanish, Portuguese, French, German). Missing keys fall back
    /// to English, then to the key itself. Object names are localised with keys "obj_{id}".
    /// For larger scopes migrate to the Unity Localization package; call sites only use Loc.T.
    /// </summary>
    public static class Loc
    {
        public static readonly string[] Languages = { "en", "es", "pt", "fr", "de" };
        public static readonly string[] LanguageNames = { "English", "Español", "Português", "Français", "Deutsch" };

        static string _lang = "en";
        static Dictionary<string, string>[] _tables;

        public static string Current { get { return _lang; } }

        public static void SetLanguage(string code)
        {
            EnsureTables();
            _lang = System.Array.IndexOf(Languages, code) >= 0 ? code : "en";
        }

        /// <summary>Picks the device language if supported.</summary>
        public static string DetectDeviceLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                default: return "en";
            }
        }

        public static string T(string key)
        {
            EnsureTables();
            int idx = System.Array.IndexOf(Languages, _lang);
            string v;
            if (idx > 0 && _tables[idx].TryGetValue(key, out v)) return v;
            if (_tables[0].TryGetValue(key, out v)) return v;
            return key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static string ObjectName(string objectId, string fallback)
        {
            var key = "obj_" + objectId;
            var v = T(key);
            return v == key ? fallback : v;
        }

        static void EnsureTables()
        {
            if (_tables != null) return;
            _tables = new[] { En(), Es(), Pt(), Fr(), De() };
        }

        static Dictionary<string, string> En()
        {
            return new Dictionary<string, string>
            {
                { "subtitle", "Everyday Objects" },
                { "play", "PLAY" },
                { "level_n", "Level {0}" },
                { "daily", "Daily" },
                { "daily_challenge", "Daily Challenge" },
                { "collection", "Collection" },
                { "settings", "Settings" },
                { "streak_n", "{0}-day streak" },
                { "best_streak_n", "Best: {0}" },
                { "done_today", "Done today" },
                { "reward_n", "+{0}" },
                { "mystery", "???" },
                { "pieces_left", "{0} left" },
                { "hint", "Hint" },
                { "paused", "Paused" },
                { "resume", "Resume" },
                { "restart", "Restart" },
                { "home", "Home" },
                { "continue", "Continue" },
                { "double_coins", "x2 Coins" },
                { "complete_1", "Brilliant!" },
                { "complete_2", "Wonderful!" },
                { "complete_3", "Superb!" },
                { "complete_4", "Nicely done!" },
                { "new_in_room", "New in {0}!" },
                { "bonus_hint", "+{0} free hint" },
                { "campaign_done", "You solved every object! New daily puzzles await." },
                { "music", "Music" },
                { "sound", "Sound effects" },
                { "vibration", "Vibration" },
                { "graphics", "Graphics" },
                { "gfx_auto", "Auto" },
                { "gfx_smooth", "Smooth" },
                { "gfx_battery", "Battery" },
                { "language", "Language" },
                { "privacy_policy", "Privacy Policy" },
                { "privacy_options", "Ad privacy choices" },
                { "remove_ads", "Remove Ads" },
                { "restore_purchases", "Restore Purchases" },
                { "close", "Close" },
                { "ok", "OK" },
                { "cancel", "Cancel" },
                { "no_hints_title", "Out of hints" },
                { "no_hints_body", "Get a hint for {0} coins or watch a short optional video." },
                { "buy_hint", "Use {0} coins" },
                { "watch_video", "Watch video" },
                { "not_enough_coins", "Not enough coins" },
                { "ad_unavailable", "No video available right now. Try again later." },
                { "login_title", "Daily Reward" },
                { "login_body", "Day {0} of 7 — come back every day for bigger rewards!" },
                { "claim", "Claim" },
                { "today", "Today" },
                { "special_collectible", "Special collectible!" },
                { "come_back_tomorrow", "Come back tomorrow for a new puzzle" },
                { "history", "History" },
                { "no_history", "Complete a daily challenge to start your history." },
                { "locked", "Locked" },
                { "unlock_by_level", "Solve level {0}" },
                { "unlock_by_daily", "Daily challenge" },
                { "solved_variants", "Solved {0}/3" },
                { "drag_to_rotate", "Drag to rotate · Pinch to zoom" },
                { "tutorial_tap", "Tap a piece to slide it out along its arrow" },
                { "tutorial_blocked", "Blocked! Free the way first, then try again." },
                { "got_it", "Got it" },
                { "mech_locked_title", "Locked pieces" },
                { "mech_locked_body", "Caged pieces can't move until the highlighted key piece has escaped." },
                { "mech_chain_title", "Chain arrows" },
                { "mech_chain_body", "Gold arrows are linked: when one escapes, its partner tries to follow." },
                { "mech_hidden_title", "Hidden arrows" },
                { "mech_hidden_body", "Pieces marked with diamonds hide their arrow. Rotate the object to look at them." },
                { "mech_rotating_title", "Rotating arrows" },
                { "mech_rotating_body", "Purple arrows turn to their next direction each time they're blocked." },
                { "mech_color_title", "Colour gates" },
                { "mech_color_body", "Only pieces with a matching arrow colour can pass through a gate." },
                { "quit_level_title", "Leave level?" },
                { "quit_level_body", "Your progress on this puzzle will be lost." },
                { "leave", "Leave" },
                { "stay", "Stay" },
                { "loading", "Loading…" },
                { "version", "Version {0}" },
                { "purchase_failed", "Purchase could not be completed." },
                { "purchase_restored", "Purchases restored." },
                { "room_Kitchen", "Kitchen" },
                { "room_LivingRoom", "Living Room" },
                { "room_Bedroom", "Bedroom" },
                { "room_Office", "Office" },
                { "room_Garage", "Garage" },
                { "room_Electronics", "Electronics" },
                { "room_Toys", "Toys" },
                { "room_Outdoor", "Outdoor" },
                { "tier_Beginner", "Beginner" },
                { "tier_Easy", "Easy" },
                { "tier_Medium", "Medium" },
                { "tier_Hard", "Hard" },
                { "tier_Expert", "Expert" },
                { "practice_level", "Practice level · no move limit" },
                { "hard", "HARD" },
                { "super_hard", "SUPER HARD" },
                { "tip_hard", "Hard level! Look all around the object before you tap." },
                { "tip_super_hard", "Super Hard level! Only a few pieces can move. Search every side." },
                { "tutorial_hearts", "From now on you have {0} hearts per level. Every blocked tap costs one!" },
                { "level_map", "Level Map" },
                { "world_1", "World 1 · Home Sweet Home" },
                { "world_2", "World 2 · Gadget Town" },
                { "world_3", "World 3 · Expert Heights" },
                { "life", "life" },
                { "full", "Full" },
                { "wait", "Wait" },
                { "lives_n", "Lives {0}/{1}" },
                { "lives_full", "Lives full" },
                { "lives_full_body", "You lose a life when you fail a level or leave it after moving. Lost lives come back one every 30 minutes." },
                { "out_of_lives", "Out of lives" },
                { "next_life_in", "Next life in {0}. Lives come back one every 30 minutes (max 5)." },
                { "ad_for_life", "Watch ad · +1 life ({0} left today)" },
                { "refill_lives", "Refill all lives · {0} coins" },
                { "lives_refilled", "Lives refilled!" },
                { "ask_friends", "Ask friends for lives" },
                { "asked_friends", "Asked your friends for lives" },
                { "friends_unavailable", "Friends aren't connected yet." },
                { "out_of_moves", "Out of moves!" },
                { "out_of_moves_body", "You used all your hearts for this level. Trying again or giving up costs one life (you have {0})." },
                { "out_of_moves_daily", "You used all your hearts. Try today's puzzle again." },
                { "continue_ad", "Watch ad · +{0} moves & continue" },
                { "try_again", "Try again" },
                { "try_again_cost", "Try again (−1 life)" },
                { "give_up_cost", "Give up (−1 life)" },
                { "restart_title", "Restart level?" },
                { "costs_life_body", "You'll lose a life if you leave or restart now." },
                { "keep_playing", "Keep playing" },
                { "restart_cost", "Restart (−1 life)" },
                { "leave_cost", "Leave (−1 life)" },
                { "hint_ad_body", "Your {0} free hints are used up. Watch a short video to get 1 hint ({1} left today)." },
                { "hint_ads_done", "You've used today's {0} video hints. New ones tomorrow." },
                { "watch_video_hint", "Watch ad · +1 hint" },
                { "obj_coffee_mug", "Coffee Mug" },
                { "obj_water_bottle", "Water Bottle" },
                { "obj_table_lamp", "Table Lamp" },
                { "obj_chair", "Chair" },
                { "obj_alarm_clock", "Alarm Clock" },
                { "obj_headphones", "Headphones" },
                { "obj_backpack", "Backpack" },
                { "obj_camera", "Camera" },
                { "obj_bicycle", "Bicycle" },
                { "obj_washing_machine", "Washing Machine" },
                { "obj_teapot", "Teapot" },
                { "obj_toy_robot", "Toy Robot" },
                { "obj_retro_radio", "Retro Radio" },
                { "obj_desk_fan", "Desk Fan" },
                { "obj_laptop", "Laptop" },
                { "obj_toolbox", "Toolbox" },
            };
        }

        static Dictionary<string, string> Es()
        {
            return new Dictionary<string, string>
            {
                { "subtitle", "Objetos cotidianos" }, { "play", "JUGAR" }, { "level_n", "Nivel {0}" }, { "daily", "Diario" },
                { "daily_challenge", "Reto diario" }, { "collection", "Colección" }, { "settings", "Ajustes" },
                { "streak_n", "Racha de {0} días" }, { "best_streak_n", "Mejor: {0}" }, { "done_today", "Hecho hoy" },
                { "pieces_left", "Quedan {0}" }, { "hint", "Pista" }, { "paused", "Pausa" }, { "resume", "Continuar" },
                { "restart", "Reiniciar" }, { "home", "Inicio" }, { "continue", "Continuar" }, { "double_coins", "x2 monedas" },
                { "complete_1", "¡Brillante!" }, { "complete_2", "¡Maravilloso!" }, { "complete_3", "¡Excelente!" }, { "complete_4", "¡Bien hecho!" },
                { "new_in_room", "¡Nuevo en {0}!" }, { "bonus_hint", "+{0} pista gratis" },
                { "campaign_done", "¡Resolviste todos los objetos! Te esperan nuevos retos diarios." },
                { "music", "Música" }, { "sound", "Efectos de sonido" }, { "vibration", "Vibración" }, { "graphics", "Gráficos" },
                { "gfx_auto", "Auto" }, { "gfx_smooth", "Fluido" }, { "gfx_battery", "Batería" }, { "language", "Idioma" },
                { "privacy_policy", "Política de privacidad" }, { "privacy_options", "Privacidad de anuncios" },
                { "remove_ads", "Quitar anuncios" }, { "restore_purchases", "Restaurar compras" }, { "close", "Cerrar" },
                { "cancel", "Cancelar" }, { "no_hints_title", "Sin pistas" },
                { "no_hints_body", "Consigue una pista por {0} monedas o mira un video opcional." },
                { "buy_hint", "Usar {0} monedas" }, { "watch_video", "Ver video" }, { "not_enough_coins", "Monedas insuficientes" },
                { "ad_unavailable", "No hay videos disponibles ahora. Inténtalo más tarde." },
                { "login_title", "Recompensa diaria" }, { "login_body", "Día {0} de 7: ¡vuelve cada día para premios mayores!" },
                { "claim", "Reclamar" }, { "today", "Hoy" }, { "special_collectible", "¡Coleccionable especial!" },
                { "come_back_tomorrow", "Vuelve mañana para un nuevo reto" }, { "history", "Historial" },
                { "no_history", "Completa un reto diario para empezar tu historial." }, { "locked", "Bloqueado" },
                { "unlock_by_level", "Resuelve el nivel {0}" }, { "unlock_by_daily", "Reto diario" }, { "solved_variants", "Resuelto {0}/3" },
                { "drag_to_rotate", "Arrastra para girar · Pellizca para zoom" },
                { "tutorial_tap", "Toca una pieza para deslizarla en la dirección de su flecha" },
                { "tutorial_blocked", "¡Bloqueada! Libera el camino y vuelve a intentarlo." }, { "got_it", "Entendido" },
                { "mech_locked_title", "Piezas bloqueadas" }, { "mech_locked_body", "Las piezas enjauladas no se mueven hasta que escape su pieza llave." },
                { "mech_chain_title", "Flechas en cadena" }, { "mech_chain_body", "Las flechas doradas están unidas: cuando una sale, su pareja intenta seguirla." },
                { "mech_hidden_title", "Flechas ocultas" }, { "mech_hidden_body", "Las piezas con rombos esconden su flecha. Gira el objeto para verla." },
                { "mech_rotating_title", "Flechas giratorias" }, { "mech_rotating_body", "Las flechas moradas giran a su siguiente dirección cada vez que se bloquean." },
                { "mech_color_title", "Puertas de color" }, { "mech_color_body", "Solo las piezas con flecha del mismo color pueden cruzar una puerta." },
                { "quit_level_title", "¿Salir del nivel?" }, { "quit_level_body", "Perderás el progreso de este puzle." },
                { "leave", "Salir" }, { "stay", "Quedarse" }, { "loading", "Cargando…" }, { "version", "Versión {0}" },
                { "room_Kitchen", "Cocina" }, { "room_LivingRoom", "Salón" }, { "room_Bedroom", "Dormitorio" }, { "room_Office", "Oficina" },
                { "room_Garage", "Garaje" }, { "room_Electronics", "Electrónica" }, { "room_Toys", "Juguetes" }, { "room_Outdoor", "Exterior" },
                { "tier_Beginner", "Principiante" }, { "tier_Easy", "Fácil" }, { "tier_Medium", "Medio" }, { "tier_Hard", "Difícil" }, { "tier_Expert", "Experto" },
                { "obj_coffee_mug", "Taza de café" }, { "obj_water_bottle", "Botella de agua" }, { "obj_table_lamp", "Lámpara de mesa" },
                { "obj_chair", "Silla" }, { "obj_alarm_clock", "Despertador" }, { "obj_headphones", "Auriculares" }, { "obj_backpack", "Mochila" },
                { "obj_camera", "Cámara" }, { "obj_bicycle", "Bicicleta" }, { "obj_washing_machine", "Lavadora" }, { "obj_teapot", "Tetera" },
                { "obj_toy_robot", "Robot de juguete" }, { "obj_retro_radio", "Radio retro" }, { "obj_desk_fan", "Ventilador" },
                { "obj_laptop", "Portátil" }, { "obj_toolbox", "Caja de herramientas" },
            };
        }

        static Dictionary<string, string> Pt()
        {
            return new Dictionary<string, string>
            {
                { "subtitle", "Objetos do dia a dia" }, { "play", "JOGAR" }, { "level_n", "Nível {0}" }, { "daily", "Diário" },
                { "daily_challenge", "Desafio diário" }, { "collection", "Coleção" }, { "settings", "Configurações" },
                { "streak_n", "Sequência de {0} dias" }, { "best_streak_n", "Melhor: {0}" }, { "done_today", "Feito hoje" },
                { "pieces_left", "Faltam {0}" }, { "hint", "Dica" }, { "paused", "Pausado" }, { "resume", "Continuar" },
                { "restart", "Reiniciar" }, { "home", "Início" }, { "continue", "Continuar" }, { "double_coins", "x2 moedas" },
                { "complete_1", "Brilhante!" }, { "complete_2", "Maravilhoso!" }, { "complete_3", "Excelente!" }, { "complete_4", "Muito bem!" },
                { "new_in_room", "Novo em {0}!" }, { "bonus_hint", "+{0} dica grátis" },
                { "campaign_done", "Você resolveu todos os objetos! Novos desafios diários te esperam." },
                { "music", "Música" }, { "sound", "Efeitos sonoros" }, { "vibration", "Vibração" }, { "graphics", "Gráficos" },
                { "gfx_auto", "Auto" }, { "gfx_smooth", "Fluido" }, { "gfx_battery", "Bateria" }, { "language", "Idioma" },
                { "privacy_policy", "Política de privacidade" }, { "privacy_options", "Privacidade de anúncios" },
                { "remove_ads", "Remover anúncios" }, { "restore_purchases", "Restaurar compras" }, { "close", "Fechar" },
                { "cancel", "Cancelar" }, { "no_hints_title", "Sem dicas" },
                { "no_hints_body", "Ganhe uma dica por {0} moedas ou assista a um vídeo opcional." },
                { "buy_hint", "Usar {0} moedas" }, { "watch_video", "Ver vídeo" }, { "not_enough_coins", "Moedas insuficientes" },
                { "ad_unavailable", "Nenhum vídeo disponível agora. Tente mais tarde." },
                { "login_title", "Recompensa diária" }, { "login_body", "Dia {0} de 7 — volte todo dia para prêmios maiores!" },
                { "claim", "Resgatar" }, { "today", "Hoje" }, { "special_collectible", "Colecionável especial!" },
                { "come_back_tomorrow", "Volte amanhã para um novo desafio" }, { "history", "Histórico" },
                { "no_history", "Complete um desafio diário para iniciar seu histórico." }, { "locked", "Bloqueado" },
                { "unlock_by_level", "Resolva o nível {0}" }, { "unlock_by_daily", "Desafio diário" }, { "solved_variants", "Resolvido {0}/3" },
                { "drag_to_rotate", "Arraste para girar · Pinça para zoom" },
                { "tutorial_tap", "Toque numa peça para deslizá-la na direção da seta" },
                { "tutorial_blocked", "Bloqueada! Libere o caminho e tente de novo." }, { "got_it", "Entendi" },
                { "mech_locked_title", "Peças trancadas" }, { "mech_locked_body", "Peças enjauladas só se movem depois que a peça-chave sair." },
                { "mech_chain_title", "Setas em corrente" }, { "mech_chain_body", "Setas douradas estão ligadas: quando uma sai, a parceira tenta seguir." },
                { "mech_hidden_title", "Setas ocultas" }, { "mech_hidden_body", "Peças com losangos escondem a seta. Gire o objeto para vê-la." },
                { "mech_rotating_title", "Setas giratórias" }, { "mech_rotating_body", "Setas roxas giram para a próxima direção quando bloqueadas." },
                { "mech_color_title", "Portões de cor" }, { "mech_color_body", "Só peças com seta da mesma cor atravessam um portão." },
                { "quit_level_title", "Sair do nível?" }, { "quit_level_body", "O progresso deste quebra-cabeça será perdido." },
                { "leave", "Sair" }, { "stay", "Ficar" }, { "loading", "Carregando…" }, { "version", "Versão {0}" },
                { "room_Kitchen", "Cozinha" }, { "room_LivingRoom", "Sala" }, { "room_Bedroom", "Quarto" }, { "room_Office", "Escritório" },
                { "room_Garage", "Garagem" }, { "room_Electronics", "Eletrônicos" }, { "room_Toys", "Brinquedos" }, { "room_Outdoor", "Ar livre" },
                { "tier_Beginner", "Iniciante" }, { "tier_Easy", "Fácil" }, { "tier_Medium", "Médio" }, { "tier_Hard", "Difícil" }, { "tier_Expert", "Especialista" },
                { "obj_coffee_mug", "Caneca de café" }, { "obj_water_bottle", "Garrafa de água" }, { "obj_table_lamp", "Luminária" },
                { "obj_chair", "Cadeira" }, { "obj_alarm_clock", "Despertador" }, { "obj_headphones", "Fones de ouvido" }, { "obj_backpack", "Mochila" },
                { "obj_camera", "Câmera" }, { "obj_bicycle", "Bicicleta" }, { "obj_washing_machine", "Máquina de lavar" }, { "obj_teapot", "Bule" },
                { "obj_toy_robot", "Robô de brinquedo" }, { "obj_retro_radio", "Rádio retrô" }, { "obj_desk_fan", "Ventilador" },
                { "obj_laptop", "Notebook" }, { "obj_toolbox", "Caixa de ferramentas" },
            };
        }

        static Dictionary<string, string> Fr()
        {
            return new Dictionary<string, string>
            {
                { "subtitle", "Objets du quotidien" }, { "play", "JOUER" }, { "level_n", "Niveau {0}" }, { "daily", "Quotidien" },
                { "daily_challenge", "Défi du jour" }, { "collection", "Collection" }, { "settings", "Réglages" },
                { "streak_n", "Série de {0} jours" }, { "best_streak_n", "Record : {0}" }, { "done_today", "Fait aujourd'hui" },
                { "pieces_left", "Encore {0}" }, { "hint", "Indice" }, { "paused", "Pause" }, { "resume", "Reprendre" },
                { "restart", "Recommencer" }, { "home", "Accueil" }, { "continue", "Continuer" }, { "double_coins", "x2 pièces" },
                { "complete_1", "Brillant !" }, { "complete_2", "Merveilleux !" }, { "complete_3", "Superbe !" }, { "complete_4", "Bien joué !" },
                { "new_in_room", "Nouveau : {0} !" }, { "bonus_hint", "+{0} indice offert" },
                { "campaign_done", "Tous les objets sont résolus ! De nouveaux défis quotidiens vous attendent." },
                { "music", "Musique" }, { "sound", "Effets sonores" }, { "vibration", "Vibration" }, { "graphics", "Graphismes" },
                { "gfx_auto", "Auto" }, { "gfx_smooth", "Fluide" }, { "gfx_battery", "Batterie" }, { "language", "Langue" },
                { "privacy_policy", "Politique de confidentialité" }, { "privacy_options", "Confidentialité des pubs" },
                { "remove_ads", "Supprimer les pubs" }, { "restore_purchases", "Restaurer les achats" }, { "close", "Fermer" },
                { "cancel", "Annuler" }, { "no_hints_title", "Plus d'indices" },
                { "no_hints_body", "Obtenez un indice pour {0} pièces ou regardez une courte vidéo facultative." },
                { "buy_hint", "Utiliser {0} pièces" }, { "watch_video", "Voir la vidéo" }, { "not_enough_coins", "Pas assez de pièces" },
                { "ad_unavailable", "Aucune vidéo disponible. Réessayez plus tard." },
                { "login_title", "Récompense du jour" }, { "login_body", "Jour {0} sur 7 — revenez chaque jour pour plus de cadeaux !" },
                { "claim", "Récupérer" }, { "today", "Aujourd'hui" }, { "special_collectible", "Objet de collection spécial !" },
                { "come_back_tomorrow", "Revenez demain pour un nouveau défi" }, { "history", "Historique" },
                { "no_history", "Terminez un défi du jour pour démarrer votre historique." }, { "locked", "Verrouillé" },
                { "unlock_by_level", "Résolvez le niveau {0}" }, { "unlock_by_daily", "Défi du jour" }, { "solved_variants", "Résolu {0}/3" },
                { "drag_to_rotate", "Glissez pour tourner · Pincez pour zoomer" },
                { "tutorial_tap", "Touchez une pièce pour la faire glisser dans le sens de sa flèche" },
                { "tutorial_blocked", "Bloquée ! Libérez le passage puis réessayez." }, { "got_it", "Compris" },
                { "mech_locked_title", "Pièces verrouillées" }, { "mech_locked_body", "Les pièces en cage ne bougent pas tant que leur pièce clé n'est pas sortie." },
                { "mech_chain_title", "Flèches enchaînées" }, { "mech_chain_body", "Les flèches dorées sont liées : quand l'une sort, sa partenaire tente de suivre." },
                { "mech_hidden_title", "Flèches cachées" }, { "mech_hidden_body", "Les pièces à losanges cachent leur flèche. Tournez l'objet pour la voir." },
                { "mech_rotating_title", "Flèches tournantes" }, { "mech_rotating_body", "Les flèches violettes tournent vers la direction suivante à chaque blocage." },
                { "mech_color_title", "Portes de couleur" }, { "mech_color_body", "Seules les pièces de la même couleur peuvent traverser une porte." },
                { "quit_level_title", "Quitter le niveau ?" }, { "quit_level_body", "Votre progression sur ce puzzle sera perdue." },
                { "leave", "Quitter" }, { "stay", "Rester" }, { "loading", "Chargement…" }, { "version", "Version {0}" },
                { "room_Kitchen", "Cuisine" }, { "room_LivingRoom", "Salon" }, { "room_Bedroom", "Chambre" }, { "room_Office", "Bureau" },
                { "room_Garage", "Garage" }, { "room_Electronics", "Électronique" }, { "room_Toys", "Jouets" }, { "room_Outdoor", "Extérieur" },
                { "tier_Beginner", "Débutant" }, { "tier_Easy", "Facile" }, { "tier_Medium", "Moyen" }, { "tier_Hard", "Difficile" }, { "tier_Expert", "Expert" },
                { "obj_coffee_mug", "Tasse à café" }, { "obj_water_bottle", "Gourde" }, { "obj_table_lamp", "Lampe de chevet" },
                { "obj_chair", "Chaise" }, { "obj_alarm_clock", "Réveil" }, { "obj_headphones", "Casque audio" }, { "obj_backpack", "Sac à dos" },
                { "obj_camera", "Appareil photo" }, { "obj_bicycle", "Vélo" }, { "obj_washing_machine", "Lave-linge" }, { "obj_teapot", "Théière" },
                { "obj_toy_robot", "Robot jouet" }, { "obj_retro_radio", "Radio rétro" }, { "obj_desk_fan", "Ventilateur" },
                { "obj_laptop", "Ordinateur portable" }, { "obj_toolbox", "Boîte à outils" },
            };
        }

        static Dictionary<string, string> De()
        {
            return new Dictionary<string, string>
            {
                { "subtitle", "Alltagsgegenstände" }, { "play", "SPIELEN" }, { "level_n", "Level {0}" }, { "daily", "Täglich" },
                { "daily_challenge", "Tagesrätsel" }, { "collection", "Sammlung" }, { "settings", "Einstellungen" },
                { "streak_n", "{0} Tage in Folge" }, { "best_streak_n", "Rekord: {0}" }, { "done_today", "Heute erledigt" },
                { "pieces_left", "Noch {0}" }, { "hint", "Tipp" }, { "paused", "Pause" }, { "resume", "Weiter" },
                { "restart", "Neustart" }, { "home", "Start" }, { "continue", "Weiter" }, { "double_coins", "x2 Münzen" },
                { "complete_1", "Brillant!" }, { "complete_2", "Wunderbar!" }, { "complete_3", "Großartig!" }, { "complete_4", "Gut gemacht!" },
                { "new_in_room", "Neu in {0}!" }, { "bonus_hint", "+{0} Gratis-Tipp" },
                { "campaign_done", "Alle Gegenstände gelöst! Neue Tagesrätsel warten." },
                { "music", "Musik" }, { "sound", "Soundeffekte" }, { "vibration", "Vibration" }, { "graphics", "Grafik" },
                { "gfx_auto", "Auto" }, { "gfx_smooth", "Flüssig" }, { "gfx_battery", "Akku" }, { "language", "Sprache" },
                { "privacy_policy", "Datenschutzerklärung" }, { "privacy_options", "Werbe-Datenschutz" },
                { "remove_ads", "Werbung entfernen" }, { "restore_purchases", "Käufe wiederherstellen" }, { "close", "Schließen" },
                { "cancel", "Abbrechen" }, { "no_hints_title", "Keine Tipps mehr" },
                { "no_hints_body", "Hol dir einen Tipp für {0} Münzen oder sieh dir ein optionales Video an." },
                { "buy_hint", "{0} Münzen nutzen" }, { "watch_video", "Video ansehen" }, { "not_enough_coins", "Nicht genug Münzen" },
                { "ad_unavailable", "Gerade kein Video verfügbar. Versuche es später." },
                { "login_title", "Tagesbelohnung" }, { "login_body", "Tag {0} von 7 – komm täglich wieder für größere Belohnungen!" },
                { "claim", "Abholen" }, { "today", "Heute" }, { "special_collectible", "Besonderes Sammelobjekt!" },
                { "come_back_tomorrow", "Komm morgen für ein neues Rätsel wieder" }, { "history", "Verlauf" },
                { "no_history", "Löse ein Tagesrätsel, um deinen Verlauf zu starten." }, { "locked", "Gesperrt" },
                { "unlock_by_level", "Löse Level {0}" }, { "unlock_by_daily", "Tagesrätsel" }, { "solved_variants", "Gelöst {0}/3" },
                { "drag_to_rotate", "Ziehen zum Drehen · Spreizen zum Zoomen" },
                { "tutorial_tap", "Tippe ein Teil an, um es in Pfeilrichtung herauszuschieben" },
                { "tutorial_blocked", "Blockiert! Mach erst den Weg frei und versuche es erneut." }, { "got_it", "Verstanden" },
                { "mech_locked_title", "Gesperrte Teile" }, { "mech_locked_body", "Teile im Käfig bewegen sich erst, wenn ihr Schlüsselteil entkommen ist." },
                { "mech_chain_title", "Kettenpfeile" }, { "mech_chain_body", "Goldene Pfeile sind verbunden: Entkommt einer, versucht sein Partner zu folgen." },
                { "mech_hidden_title", "Versteckte Pfeile" }, { "mech_hidden_body", "Teile mit Rauten verbergen ihren Pfeil. Dreh das Objekt, um ihn zu sehen." },
                { "mech_rotating_title", "Drehpfeile" }, { "mech_rotating_body", "Lila Pfeile drehen sich bei jeder Blockade in die nächste Richtung." },
                { "mech_color_title", "Farbtore" }, { "mech_color_body", "Nur Teile mit gleichfarbigem Pfeil passen durch ein Tor." },
                { "quit_level_title", "Level verlassen?" }, { "quit_level_body", "Dein Fortschritt in diesem Rätsel geht verloren." },
                { "leave", "Verlassen" }, { "stay", "Bleiben" }, { "loading", "Lädt…" }, { "version", "Version {0}" },
                { "room_Kitchen", "Küche" }, { "room_LivingRoom", "Wohnzimmer" }, { "room_Bedroom", "Schlafzimmer" }, { "room_Office", "Büro" },
                { "room_Garage", "Garage" }, { "room_Electronics", "Elektronik" }, { "room_Toys", "Spielzeug" }, { "room_Outdoor", "Draußen" },
                { "tier_Beginner", "Anfänger" }, { "tier_Easy", "Leicht" }, { "tier_Medium", "Mittel" }, { "tier_Hard", "Schwer" }, { "tier_Expert", "Experte" },
                { "obj_coffee_mug", "Kaffeetasse" }, { "obj_water_bottle", "Trinkflasche" }, { "obj_table_lamp", "Tischlampe" },
                { "obj_chair", "Stuhl" }, { "obj_alarm_clock", "Wecker" }, { "obj_headphones", "Kopfhörer" }, { "obj_backpack", "Rucksack" },
                { "obj_camera", "Kamera" }, { "obj_bicycle", "Fahrrad" }, { "obj_washing_machine", "Waschmaschine" }, { "obj_teapot", "Teekanne" },
                { "obj_toy_robot", "Spielzeugroboter" }, { "obj_retro_radio", "Retro-Radio" }, { "obj_desk_fan", "Tischventilator" },
                { "obj_laptop", "Laptop" }, { "obj_toolbox", "Werkzeugkasten" },
            };
        }
    }
}
