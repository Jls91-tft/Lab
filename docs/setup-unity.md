# Checklist de esta noche: Unity 6 + repo + MCP

> Escrito el 15/10/2026. **No he podido abrir la documentación de Unity desde mi entorno**
> (la red lo bloquea), así que los menús y comandos del MCP salen de búsquedas y hay que
> confirmarlos en pantalla. Si algo no coincide, apúntalo y lo corrijo aquí.

## 1. Versión de Unity
- [ ] Instalar **Unity Hub** (última versión).
- [ ] Instalar **Unity 6.3 LTS**, el último parche `6000.3.x` que ofrezca el Hub.
  - Por qué LTS: soporte hasta diciembre de 2027 y lo que antes soportan FishNet, Mirror y el MCP.
  - 6.6 es más nueva (*Supported*), pero el 6.7 LTS sale a finales de 2026; cuando salga, valoramos migrar (pasar de un LTS a otro es lo habitual).
  - **No** usar 6.0 LTS: su soporte termina en octubre de 2026.
- [ ] Anota la versión exacta instalada; la registro en `docs/decisiones.md`.

## 2. Módulos del instalador
| Módulo | ¿Instalar? | Por qué |
| --- | --- | --- |
| Windows Build Support (**IL2CPP**) | ✅ | Build final de Steam (más rendimiento que Mono) |
| Microsoft Visual Studio Community | ⚪ opcional | Solo si no usas VS Code. Si tu PC es Windows, IL2CPP necesita además la carga de trabajo *Desarrollo para escritorio con C++* de Visual Studio |
| Documentation | ✅ | Manual sin conexión |
| Linux Build Support (IL2CPP) | ⚪ más tarde | No hace falta para Steam Deck (Proton ejecuta la build de Windows) |
| Mac Build Support | ⚪ más tarde | Solo si vendemos en Mac |
| Android / iOS | ❌ ahora | Fase 2 (móvil) |

**Editor de código:** recomiendo **VS Code** + extensión **Unity** (incluye C# Dev Kit, gratis
para uso individual) porque convive bien con Claude Code en la terminal. En Unity:
*Edit > Preferences > External Tools > External Script Editor = Visual Studio Code*.

## 3. Crear el proyecto dentro del repo
Ruta del repo: la carpeta donde está este `docs/` (abajo, `<repo>`).

- [ ] Unity Hub → **New project** → plantilla **Universal 2D** (2D con URP).
  Si no aparece, pulsa *Download template*.
- [ ] **Project name:** `CepaCero` · **Location:** `<repo>` → resultado: `<repo>/CepaCero/`.
- [ ] **Desmarcar "Use Unity Version Control"** (usamos Git).
- [ ] *Connect to Unity Cloud*: márcalo **solo** si el MCP lo exige (ver §5); es gratis con una cuenta Unity Personal, pero no lo necesitamos para nada más.
- [ ] Crear y esperar a que abra. Luego **cerrar Unity** antes del primer commit.

### Comprobar que el `.gitignore` funciona
```bash
cd <repo>
git status --short -uall | head -30  # deben salir CepaCero/Assets, Packages, ProjectSettings…
git status --short -uall | grep -Ei 'Library|Temp|Logs|UserSettings|\.csproj|\.sln' \
  && echo "MAL: algo generado se cuela" || echo "OK"
ls CepaCero/.gitignore 2>/dev/null && echo "Unity ha creado un .gitignore propio: revisarlo o borrarlo"
```
- [ ] Ajustes del proyecto (vienen por defecto en Unity 6; solo comprobar):
  *Edit > Project Settings > Editor* → **Version Control: Visible Meta Files** y **Asset Serialization: Force Text**.

### UnityYAMLMerge (merge de escenas y prefabs)
`.gitattributes` ya marca escenas y prefabs con `merge=unityyamlmerge`. Falta decirle a Git dónde está la herramienta (solo en tu máquina):
```bash
# Windows (Git Bash). Cambia <version> por la tuya.
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/<version>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
# macOS: /Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Tools/UnityYAMLMerge
```

### Primer commit
```bash
git lfs install                 # una vez por máquina
git add CepaCero
git commit -m "Proyecto Unity vacío (Universal 2D, Unity 6000.3.x)"
git push
```

## 4. Lo que NO hay que instalar todavía
- Paquete de red ni Steamworks: esperan a la decisión de red y a la semana 2 de `docs/plan.md`.
- Assets de la Asset Store, ni gratis ni de pago: primero lo hablamos.

## 5. MCP oficial de Unity para Claude Code
Paquete **`com.unity.ai.assistant`** (Unity AI Assistant, en beta). Pasos según las guías que encontré:

- [ ] **Coste antes de nada:** al activarlo, mira si pide una suscripción, prueba o "puntos" de Unity AI.
  **Si cuesta dinero, para y me lo dices** (regla: nada de pago sin decidirlo juntos). Hay
  alternativas comunitarias gratuitas que podemos valorar.
- [ ] *Window > Package Manager* → **+** → *Add package by name…* → `com.unity.ai.assistant`.
  Si no lo encuentra, activa los paquetes preliminares: *Project Settings > Package Manager > Enable Pre-release Packages*.
- [ ] *Edit > Project Settings > AI > Unity MCP*: comprobar que el **Unity Bridge** aparece como *running*.
- [ ] En la sección **Integrations**, busca **Claude Code**. Para Claude Code la configuración
  automática no suele estar disponible: **copia el comando** que muestra el panel y ejecútalo
  en una terminal **en la raíz del repo**.
- [ ] Arranca Claude Code en la raíz del repo y, en Unity, **aprueba la conexión** en *Pending Connections*.
- [ ] Comprobación: `claude mcp list` debe mostrar el servidor de Unity como conectado, y dentro de Claude Code `/mcp` lo lista.
- [ ] Prueba final: pídeme "lee la consola de Unity y dime cuántos errores hay".

> Una guía de septiembre de 2026 menciona además un camino por línea de comandos
> (`unity mcp configure claude-code`) y un plugin oficial para Claude Code. No he podido
> confirmarlo; si en el panel ves esa opción, úsala y me cuentas.

## 6. Al terminar, pásame
1. La versión exacta de Unity (`6000.3.xxf1`).
2. La salida de `git status --short -uall | head -30`.
3. Si el MCP pide pago o no, y si conectó.
4. Cualquier error de la consola de Unity (copiar y pegar).
