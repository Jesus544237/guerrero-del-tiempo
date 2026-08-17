using System.IO;
using Ekkar.Gameplay;
using Ekkar.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Da vida a los prefabs de enemigos: les pone la vida, la cabeza y sus
    /// sonidos, con los numeros propios de cada uno. Y prepara a Ekkar para
    /// que pueda pegar y recibir.
    ///
    /// Va aparte del importador porque el importador se puede volver a lanzar
    /// cada vez que cambian las hojas, y esto son ajustes de juego que no hay
    /// que perder por regenerar el arte.
    ///
    /// Si solo quieres una cosa, esta "Ekkar/Arreglar todo": prepara los
    /// prefabs, ajusta a Ekkar y repasa las cuatro escenas de una pasada.
    /// </summary>
    public static class EnemySetup
    {
        const string PrefabRoot = "Assets/_Game/Prefabs/Enemigos";
        const string ArtRoot = "Assets/_Game/Art/Characters/Enemigos";
        const string FuentePath = "Assets/_Game/Art/Fonts/Ekkar_Pixel_SDF.asset";

        struct Ficha
        {
            public string era;
            public int vida, dano;
            public float velocidad, vision, alcance, cadencia, patrulla;
            public bool vuela, aDistancia;
        }

        /// <summary>
        /// El alcance ahora es literal: cuanto llega el golpe por delante del
        /// centro del bicho, en unidades del mundo. Antes era la mitad de la
        /// cuenta — se le sumaba el radio de un circulo — y por eso los numeros
        /// pequenos daban golpes enormes.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, Ficha> Fichas =
            new System.Collections.Generic.Dictionary<string, Ficha>
        {
            // Infanteria: lenta, pega de cerca, aguanta poco.
            ["soldado_hueso"] = new Ficha {
                era = "medieval", vida = 3, dano = 1, velocidad = 1.5f, vision = 5.5f,
                alcance = 1.6f, cadencia = 2.0f, patrulla = 3f },

            // Vuela, va directo y es rapido, pero cae con dos golpes.
            ["espectro_asedio"] = new Ficha {
                era = "medieval", vida = 2, dano = 1, velocidad = 2.3f, vision = 6.5f,
                alcance = 1.5f, cadencia = 1.7f, patrulla = 2f, vuela = true },

            // Dispara de lejos y no se acerca: obliga a cerrar distancia.
            ["ballestero_ceniciento"] = new Ficha {
                era = "medieval", vida = 2, dano = 1, velocidad = 1.2f, vision = 8f,
                alcance = 5.0f, cadencia = 2.6f, patrulla = 1.5f, aDistancia = true },

            // Mini-jefe: pega el doble, aguanta mucho y no patrulla.
            ["caballero_ceniciento"] = new Ficha {
                era = "medieval", vida = 12, dano = 2, velocidad = 1.3f, vision = 9f,
                alcance = 2.4f, cadencia = 2.6f, patrulla = 0f },

            // ---- Era Industrial ----------------------------------------
            ["automata_fundicion"] = new Ficha {
                era = "industrial", vida = 4, dano = 1, velocidad = 1.3f, vision = 5.5f,
                alcance = 1.8f, cadencia = 2.2f, patrulla = 3f },

            ["dron_vapor"] = new Ficha {
                era = "industrial", vida = 2, dano = 1, velocidad = 2.5f, vision = 7f,
                alcance = 1.4f, cadencia = 1.5f, patrulla = 2f, vuela = true },

            ["capataz_oxidado"] = new Ficha {
                era = "industrial", vida = 3, dano = 1, velocidad = 1.1f, vision = 8f,
                alcance = 5.0f, cadencia = 2.8f, patrulla = 1.5f, aDistancia = true },

            ["el_gran_yunque"] = new Ficha {
                era = "industrial", vida = 14, dano = 2, velocidad = 1.0f, vision = 9.5f,
                alcance = 2.6f, cadencia = 3.0f, patrulla = 0f },

            // ---- Futuro Digital ----------------------------------------
            ["corredor_datos"] = new Ficha {
                era = "futuro", vida = 3, dano = 1, velocidad = 3.0f, vision = 7f,
                alcance = 1.7f, cadencia = 1.4f, patrulla = 3.5f },

            ["dron_centinela"] = new Ficha {
                era = "futuro", vida = 2, dano = 1, velocidad = 2.2f, vision = 8.5f,
                alcance = 5.5f, cadencia = 2.2f, patrulla = 2.5f,
                vuela = true, aDistancia = true },

            ["torreta_holografica"] = new Ficha {
                era = "futuro", vida = 4, dano = 2, velocidad = 0f, vision = 7.5f,
                alcance = 4.5f, cadencia = 2.4f, patrulla = 0f, aDistancia = true },

            ["nemesis_digital"] = new Ficha {
                era = "futuro", vida = 13, dano = 2, velocidad = 2.4f, vision = 10f,
                alcance = 2.2f, cadencia = 2.0f, patrulla = 0f },

            // ---- Hora Cero ---------------------------------------------
            ["eco_de_ekkar"] = new Ficha {
                era = "hora_cero", vida = 4, dano = 2, velocidad = 2.6f, vision = 8f,
                alcance = 1.9f, cadencia = 1.8f, patrulla = 2.5f },

            ["fragmento_animado"] = new Ficha {
                era = "hora_cero", vida = 2, dano = 1, velocidad = 2.8f, vision = 7f,
                alcance = 1.3f, cadencia = 1.6f, patrulla = 3f, vuela = true },

            // El Senor del Tiempo: el jefe final. Flota, no patrulla y pega
            // muy fuerte, pero con cadencia lenta para que se pueda leer.
            // 24 de vida y no 30: con 30 la fase 2 quedaba a diez golpes de
            // distancia y casi nadie llegaba a verla
            ["senor_del_tiempo"] = new Ficha {
                era = "hora_cero", vida = 24, dano = 2, velocidad = 1.6f, vision = 14f,
                alcance = 3.6f, cadencia = 3.4f, patrulla = 0f, vuela = true },
        };

        static readonly System.Collections.Generic.Dictionary<string, string> Nombres =
            new System.Collections.Generic.Dictionary<string, string>
        {
            ["soldado_hueso"] = "Soldado de Hueso",
            ["espectro_asedio"] = "Espectro de Asedio",
            ["ballestero_ceniciento"] = "Ballestero Ceniciento",
            ["caballero_ceniciento"] = "Caballero Ceniciento",
            ["automata_fundicion"] = "Automata de Fundicion",
            ["dron_vapor"] = "Dron de Vapor",
            ["capataz_oxidado"] = "Capataz Oxidado",
            ["el_gran_yunque"] = "El Gran Yunque",
            ["corredor_datos"] = "Corredor de Datos",
            ["dron_centinela"] = "Dron Centinela",
            ["torreta_holografica"] = "Torreta Holografica",
            ["nemesis_digital"] = "Nemesis Digital",
            ["eco_de_ekkar"] = "Eco de Ekkar",
            ["fragmento_animado"] = "Fragmento Animado",
            ["senor_del_tiempo"] = "EL SENOR DEL TIEMPO",
        };

        /// <summary>Lo que dice el cartel de entrada de cada jefe.</summary>
        static readonly System.Collections.Generic.Dictionary<string, string> Subtitulos =
            new System.Collections.Generic.Dictionary<string, string>
        {
            ["caballero_ceniciento"] = "Guardian del Sitio Eterno",
            ["el_gran_yunque"] = "Capataz de la Fundicion de las Horas",
            ["nemesis_digital"] = "Tu propio reflejo, corrompido",
            ["senor_del_tiempo"] = "El fin de todas las eras",
        };

        // ================================================== el boton grande

        [MenuItem("Ekkar/Arreglar todo (prefabs y las 4 escenas)", priority = 1)]
        public static void ArreglarTodo()
        {
            // se abren escenas por el camino: primero que el usuario decida que
            // hace con lo que tenga a medias
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string volverA = EditorSceneManager.GetActiveScene().path;

            PrepararPrefabs();

            RepasaEscena("Assets/_Game/Scenes/01_EdadMedia_SitioEterno.unity", PoblarMedieval);
            RepasaEscena("Assets/_Game/Scenes/02_EraIndustrial_FundicionDeLasHoras.unity", PoblarIndustrial);
            RepasaEscena("Assets/_Game/Scenes/03_FuturoDigital_NeonSinManana.unity", PoblarFuturo);
            RepasaEscena("Assets/_Game/Scenes/04_HoraCero_VacioEntreSegundos.unity", PoblarHoraCero);

            if (!string.IsNullOrEmpty(volverA) && File.Exists(Path.GetFullPath(volverA)))
                EditorSceneManager.OpenScene(volverA, OpenSceneMode.Single);

            Debug.Log("[Ekkar] Todo repasado: prefabs, Ekkar, enemigos y HUD en las cuatro escenas.");
        }

        /// <summary>Abre la escena, hace lo que le digan y la guarda.</summary>
        static void RepasaEscena(string ruta, System.Action trabajo)
        {
            if (!File.Exists(Path.GetFullPath(ruta)))
            {
                Debug.LogWarning($"[Ekkar] No encuentro {ruta}");
                return;
            }
            var escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            trabajo();
            EditorSceneManager.SaveScene(escena);
        }

        // ==================================================== los prefabs

        [MenuItem("Ekkar/Enemigos/Preparar prefabs para el combate", priority = 41)]
        public static void PrepararPrefabs()
        {
            int hechos = 0;
            foreach (var par in Fichas)
            {
                string path = $"{PrefabRoot}/{par.Key}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[Ekkar] Falta el prefab {path}. Importa primero las animaciones.");
                    continue;
                }

                var go = PrefabUtility.LoadPrefabContents(path);
                Configura(go, par.Key, par.Value);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                PrefabUtility.UnloadPrefabContents(go);
                hechos++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Ekkar] {hechos} enemigos listos para pelear.");
        }

        static void Configura(GameObject go, string nombre, Ficha f)
        {
            var vida = Asegura<Damageable>(go);
            var so = new SerializedObject(vida);
            so.FindProperty("bando").enumValueIndex = (int)Damageable.Bando.Enemigo;
            so.FindProperty("vidaMaxima").intValue = f.vida;
            so.FindProperty("animator").objectReferenceValue = go.GetComponentInChildren<Animator>();
            so.FindProperty("parpadeo").objectReferenceValue = go.GetComponentInChildren<SpriteRenderer>();
            so.FindProperty("sonidoDano").objectReferenceValue = Sonido(f.era, nombre, "dano");
            so.FindProperty("sonidoMuerte").objectReferenceValue = Sonido(f.era, nombre, "muerte");
            so.ApplyModifiedPropertiesWithoutUndo();

            // el cuerpo primero: la caja del golpe se saca de su tamano real
            Vector2 cuerpo = AjustaColisionador(go, f.era, nombre);

            var brain = Asegura<EnemyBrain>(go);
            bool esJefeFinal = nombre == "senor_del_tiempo";
            var bo = new SerializedObject(brain);
            bo.FindProperty("velocidad").floatValue = f.velocidad;
            bo.FindProperty("radioPatrulla").floatValue = f.patrulla;
            bo.FindProperty("vuela").boolValue = f.vuela;
            bo.FindProperty("radioVision").floatValue = f.vision;
            bo.FindProperty("alcance").floatValue = f.alcance;
            bo.FindProperty("cadencia").floatValue = f.cadencia;
            bo.FindProperty("dano").intValue = f.dano;
            bo.FindProperty("aDistancia").boolValue = f.aDistancia;
            // la caja del golpe cubre justo su cuerpo, ni mas ni menos
            bo.FindProperty("alturaGolpe").floatValue = cuerpo.y * 0.5f;
            bo.FindProperty("cajaArriba").floatValue = cuerpo.y * 0.55f;
            bo.FindProperty("cajaAbajo").floatValue = cuerpo.y * 0.55f;
            bo.FindProperty("animator").objectReferenceValue = go.GetComponentInChildren<Animator>();
            bo.FindProperty("sprite").objectReferenceValue = go.GetComponentInChildren<SpriteRenderer>();

            bool esMiniJefe0 = f.vida >= 10;
            // los enemigos normales sueltan reliquias de vez en cuando; los
            // campeones siempre, y varias: es la unica forma de llegar entero
            // al siguiente tramo del nivel
            bo.FindProperty("sueltaReliquia").floatValue = esMiniJefe0 ? 1f : 0.28f;
            bo.FindProperty("cuantasReliquias").intValue = esMiniJefe0 ? 3 : 1;
            // el campeon tiene que quedarse en pantalla lo que dura su muerte,
            // y lo suficiente para que BossDefeat avise al flujo del nivel
            bo.FindProperty("tardaEnIrse").floatValue = esMiniJefe0 ? 3.5f : 1.2f;

            if (esJefeFinal)
            {
                // no tiene idle ni caminar a secas: los suyos son de fase
                bo.FindProperty("estadoIdle").stringValue = "idle_fase1";
                bo.FindProperty("estadoAndar").stringValue = "idle_fase1";
                bo.FindProperty("estadoAtaque").stringValue = "ataque_pendulo";
                Lista(bo.FindProperty("ataques"), "ataque_pendulo", "ataque_agujas", "ataque_eco");
                // sus habilidades vuelan: no hace falta tenerlo pegado para que
                // te lance agujas o te abra el suelo debajo
                bo.FindProperty("alcanceHabilidades").floatValue = 9f;
            }
            bo.ApplyModifiedPropertiesWithoutUndo();

            // el repertorio de verdad: cada animacion saca algo distinto
            if (esJefeFinal) Asegura<BossAttackPatterns>(go);

            // el cartel con el nombre: los mini-jefes lo llevan siempre
            bool esMiniJefe = f.vida >= 10;
            var cartel = Asegura<NameTag>(go);
            var no = new SerializedObject(cartel);
            no.FindProperty("nombre").stringValue =
                Nombres.TryGetValue(nombre, out var bonito) ? bonito : nombre;
            no.FindProperty("siempreVisible").boolValue = esMiniJefe && !esJefeFinal;
            no.FindProperty("conBarra").boolValue = false;   // los jefes usan la barra grande
            no.FindProperty("fuente").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(FuentePath);
            no.ApplyModifiedPropertiesWithoutUndo();

            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
                sr.sortingOrder = 50;

            // los campeones cierran el nivel al caer, y ademas tienen pelea
            if (esMiniJefe || esJefeFinal)
            {
                var fin = Asegura<BossDefeat>(go);
                var fo = new SerializedObject(fin);
                fo.FindProperty("esJefeFinal").boolValue = esJefeFinal;
                fo.FindProperty("rotulo").stringValue = esJefeFinal
                    ? "EL GRAN RELOJ VUELVE A GIRAR"
                    : "FRAGMENTO RECUPERADO";
                fo.ApplyModifiedPropertiesWithoutUndo();

                var pelea = Asegura<BossEncounter>(go);
                var eo = new SerializedObject(pelea);
                eo.FindProperty("nombre").stringValue =
                    (Nombres.TryGetValue(nombre, out var n) ? n : nombre).ToUpperInvariant();
                eo.FindProperty("subtitulo").stringValue =
                    Subtitulos.TryGetValue(nombre, out var s) ? s : "Campeon de la era";
                eo.FindProperty("radioEntrada").floatValue = esJefeFinal ? 15f : 11f;
                // solo el jefe final tiene arte de transformacion y de fase 2;
                // los mini-jefes se pelean de una sentada
                eo.FindProperty("fases").intValue = esJefeFinal ? 2 : 1;
                eo.FindProperty("dirigeElEscenario").boolValue = esJefeFinal;
                if (esJefeFinal)
                {
                    eo.FindProperty("animTransformacion").stringValue = "transformacion_fase2";
                    eo.FindProperty("duracionTransformacion").floatValue = 16f / 14f;   // 16 fotogramas a 14 fps
                    eo.FindProperty("idleSiguienteFase").stringValue = "idle_fase2";
                    eo.FindProperty("cadenciaSiguienteFase").floatValue = 2.6f;
                    eo.FindProperty("velocidadSiguienteFase").floatValue = 2.1f;
                    Lista(eo.FindProperty("ataquesSiguienteFase"),
                          "fase2_ataque_grieta", "fase2_ataque_reloj",
                          "fase2_ataque_enjambre_opcion_1", "ataque_final");
                }
                eo.ApplyModifiedPropertiesWithoutUndo();
            }

            // el cuerpo no empuja a Ekkar: el dano lo reparte el cerebro
            foreach (var col in go.GetComponentsInChildren<Collider2D>())
                col.isTrigger = true;
        }

        /// <summary>
        /// Ajusta el colisionador al bicho que de verdad se ve.
        ///
        /// El importador lo calculaba a ojo — alto del manifiesto, ancho un 38%
        /// de eso — y con los bichos anchos salia disparatado: el Senor del
        /// Tiempo tenia una capsula de dos unidades de ancho y mas alta que su
        /// propio dibujo. Por eso podias estar pegado a el sin darle: le pegabas
        /// al aire que hay entre su cuerpo y su capsula.
        ///
        /// La malla "ajustada" de Unity no sirve para medirlo: en estos sprites
        /// devuelve casi el fotograma entero. Asi que se mide donde esta la
        /// carne, contando pixeles opacos por columna y quedandose con las que
        /// llegan a la mitad de la columna mas llena. Eso separa el cuerpo de
        /// las cintas, los engranajes y el humo, que son opacos pero finos.
        /// </summary>
        /// <returns>El tamano final del colisionador.</returns>
        static Vector2 AjustaColisionador(GameObject go, string era, string nombre)
        {
            var col = Asegura<CapsuleCollider2D>(go);
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return col.size;

            var referencia = SpriteDeReferencia(era, nombre) ?? sr.sprite;
            if (referencia == null) return col.size;
            if (!MideCuerpo(referencia, out Vector2 tam, out Vector2 centro)) return col.size;

            // el sprite cuelga de un hijo que puede estar escalado o movido
            Transform t = sr.transform;
            float escalaX = t.lossyScale.x / Mathf.Max(0.0001f, go.transform.lossyScale.x);
            float escalaY = t.lossyScale.y / Mathf.Max(0.0001f, go.transform.lossyScale.y);

            // un poco mas ancho que el nucleo medido: los brazos y la ropa
            // cuentan como cuerpo aunque no sean lo mas denso del dibujo
            float ancho = Mathf.Max(0.45f, tam.x * 1.35f * escalaX);
            float alto = Mathf.Max(0.45f, tam.y * 0.95f * escalaY);

            // el desplazamiento horizontal se deja casi a cero: el sprite se
            // voltea al girar y el colisionador no, asi que uno descentrado
            // saltaria de lado cada vez que el bicho cambia de sentido
            float cx = Mathf.Clamp(centro.x * escalaX + t.localPosition.x, -0.15f, 0.15f);
            float cy = centro.y * escalaY + t.localPosition.y;

            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(ancho, alto);
            col.offset = new Vector2(cx, cy);
            return col.size;
        }

        /// <summary>
        /// El primer fotograma de la animacion mas quieta que tenga. Vale
        /// cualquiera de la hoja: en un idle todos son casi el mismo.
        /// </summary>
        static Sprite SpriteDeReferencia(string era, string nombre)
        {
            foreach (var anim in new[] { "idle_fase1", "idle", "flotar", "caminar" })
            {
                string p = $"{ArtRoot}/{era}/{nombre}/{nombre}_{anim}.png";
                if (!File.Exists(Path.GetFullPath(p))) continue;

                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    if (!(o is Sprite s)) continue;
                    // en la carpeta hay hojas sueltas que ningun manifiesto usa
                    // y que por tanto nadie ha troceado: ahi el unico "sprite"
                    // es la hoja entera, y medir eso da un cuerpo disparatado
                    if (s.rect.width >= s.texture.width - 1f) break;
                    return s;
                }
            }
            return null;
        }

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> _leidas =
            new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>
        /// Mide el cuerpo del sprite en unidades del mundo, relativo a su pivote.
        ///
        /// Las texturas estan importadas sin lectura desde script, asi que en vez
        /// de tocar el importador se descodifica el PNG del disco a una textura
        /// temporal, que nace legible.
        /// </summary>
        static bool MideCuerpo(Sprite sp, out Vector2 tam, out Vector2 centro)
        {
            tam = Vector2.zero;
            centro = Vector2.zero;

            string ruta = AssetDatabase.GetAssetPath(sp.texture);
            if (string.IsNullOrEmpty(ruta)) return false;

            if (!_leidas.TryGetValue(ruta, out var tex) || tex == null)
            {
                byte[] bytes;
                try { bytes = File.ReadAllBytes(Path.GetFullPath(ruta)); }
                catch { return false; }

                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) { Object.DestroyImmediate(tex); return false; }
                _leidas[ruta] = tex;
            }

            var r = sp.rect;
            int x0 = Mathf.Clamp((int)r.x, 0, tex.width - 1);
            int y0 = Mathf.Clamp((int)r.y, 0, tex.height - 1);
            int w = Mathf.Min((int)r.width, tex.width - x0);
            int h = Mathf.Min((int)r.height, tex.height - y0);
            if (w < 4 || h < 4) return false;

            var px = tex.GetPixels32();
            var columnas = new int[w];
            var filas = new int[h];

            for (int y = 0; y < h; y++)
            {
                int fila = (y0 + y) * tex.width + x0;
                for (int x = 0; x < w; x++)
                {
                    if (px[fila + x].a <= 100) continue;
                    columnas[x]++;
                    filas[y]++;
                }
            }

            // la mitad de la columna mas llena deja fuera cintas y humo;
            // en vertical basta un umbral bajo, que ahi si interesa la silueta
            if (!Tramo(columnas, 0.50f, out int xMin, out int xMax)) return false;
            if (!Tramo(filas, 0.12f, out int yMin, out int yMax)) return false;

            float ppu = Mathf.Max(1f, sp.pixelsPerUnit);
            tam = new Vector2((xMax - xMin + 1) / ppu, (yMax - yMin + 1) / ppu);
            centro = new Vector2(((xMin + xMax) * 0.5f - sp.pivot.x) / ppu,
                                 ((yMin + yMax) * 0.5f - sp.pivot.y) / ppu);
            return true;
        }

        /// <summary>Primer y ultimo indice que llegan a esa fraccion del maximo.</summary>
        static bool Tramo(int[] cuentas, float fraccion, out int min, out int max)
        {
            min = max = 0;
            int pico = 0;
            foreach (int v in cuentas) pico = Mathf.Max(pico, v);
            if (pico <= 0) return false;

            float corte = pico * fraccion;
            min = -1;
            for (int i = 0; i < cuentas.Length; i++)
            {
                if (cuentas[i] < corte) continue;
                if (min < 0) min = i;
                max = i;
            }
            return min >= 0 && max > min;
        }

        /// <summary>
        /// GetComponent devuelve un "falso nulo" que el operador ?? no detecta.
        /// Hay que comparar con == null, que es lo que Unity sobrecarga.
        /// </summary>
        /// <summary>Rellena un array serializado de textos.</summary>
        static void Lista(SerializedProperty prop, params string[] valores)
        {
            if (prop == null) return;
            prop.arraySize = valores.Length;
            for (int i = 0; i < valores.Length; i++)
                prop.GetArrayElementAtIndex(i).stringValue = valores[i];
        }

        static T Asegura<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c == null ? go.AddComponent<T>() : c;
        }

        static AudioClip Sonido(string era, string enemigo, string anim)
        {
            string p = $"{ArtRoot}/{era}/{enemigo}/{enemigo}_{anim}.wav";
            return File.Exists(Path.GetFullPath(p)) ? AssetDatabase.LoadAssetAtPath<AudioClip>(p) : null;
        }

        // ---------------------------------------------------------- la escena

        [MenuItem("Ekkar/Enemigos/Poblar Edad Media", priority = 42)]
        public static void PoblarMedieval()
        {
            Pobla("01_", new (string, float, float)[]
            {
                ("soldado_hueso",         14f, 0.1f),
                ("soldado_hueso",         22f, 0.1f),
                ("espectro_asedio",       30f, 1.6f),
                ("soldado_hueso",         38f, 0.1f),
                ("ballestero_ceniciento", 45f, 0.1f),
                ("espectro_asedio",       52f, 2.0f),
                ("soldado_hueso",         58f, 0.1f),
                ("ballestero_ceniciento", 64f, 0.1f),
                ("caballero_ceniciento",  74f, 0.1f),
            });
        }

        [MenuItem("Ekkar/Enemigos/Poblar Era Industrial", priority = 43)]
        public static void PoblarIndustrial()
        {
            Pobla("02_", new (string, float, float)[]
            {
                ("automata_fundicion", 15f, 0.1f),
                ("dron_vapor",         24f, 2.0f),
                ("automata_fundicion", 32f, 0.1f),
                ("capataz_oxidado",    40f, 0.1f),
                ("dron_vapor",         48f, 2.4f),
                ("automata_fundicion", 55f, 0.1f),
                ("capataz_oxidado",    62f, 0.1f),
                ("dron_vapor",         68f, 1.8f),
                ("el_gran_yunque",     76f, 0.1f),
            });
        }

        [MenuItem("Ekkar/Enemigos/Poblar Futuro Digital", priority = 44)]
        public static void PoblarFuturo()
        {
            Pobla("03_", new (string, float, float)[]
            {
                ("corredor_datos",      14f, 0.1f),
                ("dron_centinela",      22f, 2.6f),
                ("torreta_holografica", 30f, 0.1f),
                ("corredor_datos",      38f, 0.1f),
                ("dron_centinela",      45f, 3.0f),
                ("torreta_holografica", 52f, 0.1f),
                ("corredor_datos",      60f, 0.1f),
                ("dron_centinela",      67f, 2.2f),
                ("nemesis_digital",     76f, 0.1f),
            });
        }

        [MenuItem("Ekkar/Enemigos/Poblar Hora Cero", priority = 45)]
        public static void PoblarHoraCero()
        {
            Pobla("04_", new (string, float, float)[]
            {
                ("fragmento_animado", 14f, 2.0f),
                ("eco_de_ekkar",      20f, 0.1f),
                ("fragmento_animado", 26f, 2.6f),
                ("eco_de_ekkar",      34f, 0.1f),
                ("senor_del_tiempo",  46f, 1.4f),
            });
        }

        static void Pobla(string prefijoEscena, (string enemigo, float x, float y)[] reparto)
        {
            var escena = EditorSceneManager.GetActiveScene();
            if (!escena.name.StartsWith(prefijoEscena))
            {
                Debug.LogWarning($"[Ekkar] Abre antes la escena que empieza por {prefijoEscena}");
                return;
            }

            var ekkar = Object.FindAnyObjectByType<EkkarController>();
            if (ekkar != null) PreparaEkkar(ekkar.gameObject);

            var viejo = GameObject.Find("09_Enemigos");
            if (viejo != null) Object.DestroyImmediate(viejo);
            var raiz = new GameObject("09_Enemigos").transform;

            int puestos = 0;
            foreach (var (nombre, x, y) in reparto)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{nombre}.prefab");
                if (prefab == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, raiz);
                inst.transform.position = new Vector3(x, y, 0f);
                inst.name = $"{nombre}_{puestos:00}";
                puestos++;
            }

            MontaHud();

            // la meta no se abre hasta que caiga el campeon de la era. En la
            // Hora Cero faltaba el jefe final en la lista, asi que se podia
            // pasar de largo por delante de el y ganar el juego andando
            var meta = Object.FindAnyObjectByType<LevelGoal>();
            if (meta != null)
            {
                var mo = new SerializedObject(meta);
                Lista(mo.FindProperty("nombresJefe"),
                      "caballero_ceniciento", "el_gran_yunque",
                      "nemesis_digital", "senor_del_tiempo");
                mo.FindProperty("jefe").objectReferenceValue = null;   // se busca solo al arrancar
                mo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(escena);
            Debug.Log($"[Ekkar] {puestos} enemigos y el HUD completo puestos en {escena.name}.");
        }

        /// <summary>
        /// El objeto 10_HUD con sus cuatro piezas. En tiempo de ejecucion hay
        /// una red de seguridad (HudBootstrap) que anade lo que falte, pero
        /// dejarlo puesto en la escena permite verlo y tocarlo en el inspector.
        /// </summary>
        static void MontaHud()
        {
            var viejo = GameObject.Find("10_HUD");
            if (viejo != null) Object.DestroyImmediate(viejo);
            var hud = new GameObject("10_HUD");

            var fuente = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(FuentePath);

            PonFuente(hud.AddComponent<PlayerHealthBar>(), fuente);
            PonFuente(hud.AddComponent<AbilityBar>(), fuente);
            PonFuente(hud.AddComponent<TimeStopOverlay>(), fuente);

            var pausa = hud.AddComponent<PauseMenu>();
            PonFuente(pausa, fuente);
            // el panel creció al meterle la pagina de "como se juega"
            var po = new SerializedObject(pausa);
            po.FindProperty("tamanoPanel").vector2Value = new Vector2(1420f, 840f);
            po.ApplyModifiedPropertiesWithoutUndo();
        }

        static void PonFuente(Component c, TMPro.TMP_FontAsset fuente)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty("fuente");
            if (p == null) return;
            p.objectReferenceValue = fuente;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Los numeros de Ekkar. Se ponen aqui y no solo en el codigo porque
        /// las escenas ya llevan valores guardados del inspector, y esos mandan
        /// sobre lo que diga el valor por defecto del campo.
        /// </summary>
        static void PreparaEkkar(GameObject go)
        {
            // el dibujo va justo encima del cuerpo. En el Sitio Eterno estaba
            // desplazado cuatro unidades y media a la izquierda — Ekkar pegaba
            // en un sitio y se le veia en otro
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.transform != go.transform &&
                sr.transform.localPosition != Vector3.zero)
            {
                Debug.Log($"[Ekkar] El sprite estaba en {sr.transform.localPosition}; lo recoloco sobre el cuerpo.");
                sr.transform.localPosition = Vector3.zero;
            }

            var vida = Asegura<Damageable>(go);
            var so = new SerializedObject(vida);
            so.FindProperty("bando").enumValueIndex = (int)Damageable.Bando.Jugador;
            // 8 de vida y un segundo entero de gracia entre golpes: con cinco y
            // siete decimas, dos enemigos a la vez te tumbaban antes de que te
            // diera tiempo a apartarte
            so.FindProperty("vidaMaxima").intValue = 8;
            so.FindProperty("invulnerable").floatValue = 1.0f;
            so.FindProperty("animator").objectReferenceValue = go.GetComponentInChildren<Animator>();
            so.FindProperty("parpadeo").objectReferenceValue = go.GetComponentInChildren<SpriteRenderer>();
            so.FindProperty("estadoDano").stringValue = "hurt";
            so.FindProperty("estadoMuerte").stringValue = "death";
            so.ApplyModifiedPropertiesWithoutUndo();

            var combate = Asegura<PlayerCombat>(go);
            var co = new SerializedObject(combate);
            co.FindProperty("alcance").floatValue = 1.5f;
            co.FindProperty("alturaGolpe").floatValue = 1.05f;
            co.FindProperty("cajaArriba").floatValue = 0.85f;
            co.FindProperty("cajaAbajo").floatValue = 0.95f;
            co.FindProperty("manaMaximo").intValue = 10;
            co.FindProperty("manaInicial").intValue = 4;
            co.FindProperty("manaPorGolpe").intValue = 1;
            co.FindProperty("costeCargado").intValue = 4;
            co.FindProperty("danoCargado").intValue = 3;
            co.FindProperty("alcanceCargado").floatValue = 2.2f;
            co.FindProperty("curaCargado").intValue = 1;
            co.FindProperty("sprite").objectReferenceValue = go.GetComponentInChildren<SpriteRenderer>();
            co.FindProperty("respawn").objectReferenceValue = go.GetComponent<PlayerRespawn>();
            co.ApplyModifiedPropertiesWithoutUndo();

            var control = Asegura<EkkarController>(go);
            var ko = new SerializedObject(control);
            ko.FindProperty("saltosMaximos").intValue = 2;
            ko.FindProperty("graciaDespegue").floatValue = 0.14f;
            ko.FindProperty("manaDash").intValue = 1;
            ko.FindProperty("manaDetener").intValue = 3;
            ko.FindProperty("manaChrono").intValue = 6;
            ko.FindProperty("segundosDetenido").floatValue = 4f;
            ko.FindProperty("enfriamientoDetener").floatValue = 9f;
            ko.FindProperty("enfriamientoChrono").floatValue = 16f;
            ko.FindProperty("radioTormenta").floatValue = 3.2f;
            ko.FindProperty("danoTormenta").intValue = 1;
            ko.FindProperty("enfriamientoTormenta").floatValue = 1.4f;
            // el tornado del salto doble es habilidad, y las habilidades se pagan
            ko.FindProperty("manaTormenta").intValue = 2;
            // envainar sola cada cuatro segundos cortaba la animacion cada dos
            // por tres estando quieto
            ko.FindProperty("envainaTras").floatValue = 7f;
            ko.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

