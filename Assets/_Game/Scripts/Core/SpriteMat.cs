using UnityEngine;

namespace Ekkar.Core
{
    /// <summary>
    /// El material que hay que ponerle a cualquier sprite creado por codigo.
    ///
    /// El proyecto usa el renderer 2D de URP. Ahi, un SpriteRenderer nuevo nace
    /// con el material iluminado, y un sprite iluminado en una escena sin luces
    /// 2D que le den simplemente no se ve: el objeto esta, ocupa su sitio y no
    /// dibuja nada. Los sprites que vienen de una escena o un prefab ya traen su
    /// material puesto desde el editor, asi que el problema solo aparece en lo
    /// que se genera en marcha — los rayos, las saetas, los carteles.
    ///
    /// Con el material sin iluminar se ven siempre y con el color que les pongas,
    /// que para un efecto es justo lo que se quiere: un rayo no deberia
    /// oscurecerse porque a su lado no haya una farola.
    /// </summary>
    public static class SpriteMat
    {
        static Material _sinLuz;

        public static Material SinLuz
        {
            get
            {
                if (_sinLuz != null) return _sinLuz;

                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                          ?? Shader.Find("Sprites/Default");
                if (shader == null) return null;

                _sinLuz = new Material(shader) { name = "Ekkar_SpriteSinLuz" };
                return _sinLuz;
            }
        }

        /// <summary>Se lo pone al renderer, si hay material que poner.</summary>
        public static void Aplica(SpriteRenderer sr)
        {
            if (sr == null) return;
            var m = SinLuz;
            if (m != null) sr.sharedMaterial = m;
        }
    }
}
