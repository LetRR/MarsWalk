# MarsWalk: simulação de caminhada em Marte (Unity)

Este projeto é para o desafio da NASA: uma visão integrada e em camadas de uma rota na superfície de Marte, que junta dados de várias missões da NASA.

## 1. Como rodar (uns 5 minutos)

1. Abra o **Unity Hub** e crie um projeto novo com o template **3D (Built-In Render Pipeline)**. Qualquer Unity a partir do 2021.3 LTS serve, inclusive o Unity 6. O template **Universal 3D (URP)** também funciona.
2. Copie a pasta `Assets/MarsWalk` deste zip para dentro da pasta `Assets` do seu projeto.
3. Na cena aberta (SampleScene), use **GameObject → Create Empty** e dê a ele o nome `Mars`.
4. Com o `Mars` selecionado, clique em **Add Component** e escolha **MarsBootstrap**.
5. Aperte **Play**. O terreno, o astronauta, o Sol, as bases, as estações e o painel são criados automaticamente.

> A câmera e a luz que já vêm na cena são desligadas sozinhas, então não precisa apagar nada.
> Funciona tanto com o Input System novo quanto com o Input Manager antigo.

## 2. Controles

| Tecla | Ação |
|---|---|
| W A S D / setas | Andar |
| Shift | Correr (gasta mais bateria e O2) |
| Espaço | Pular (gravidade de Marte: 0,38 g) |
| Mouse | Olhar |
| **TAB** | **Painel de missão (dashboard)** |
| **M** | **Mapa em camadas, com clique para planejar a rota** |
| F | Lanterna |
| G | Simular tempestade de poeira |
| 1 / 2 | Desacelerar / acelerar o tempo |
| R | Voltar à base |
| H | Mostrar ou ocultar a ajuda |
| ESC | Fechar o painel ou soltar o mouse |

## 3. O que a simulação mostra

**HUD do capacete:** sol e hora local marciana (LMST), coordenadas, elevação, pressão, temperatura, bússola, barras de O2 e bateria, alertas e o rumo até o destino.

**Painel (TAB)** com 6 cartões:
- **Tempo:** Mars Sol Date real (algoritmo Mars24 da NASA GISS), LMST, estação, Ls, nascer e pôr do sol, hora UTC na Terra
- **Localização:** latitude e longitude reais (centro na Cratera Jezero), elevação MOLA, rumo e declividade do solo
- **Atmosfera:** pressão (que varia com a altitude, a estação e a hora), temperatura do ar e do solo, vento, opacidade da poeira τ e composição
- **Sol e radiação:** elevação e azimute do Sol, irradiância, taxa de dose e dose acumulada (RAD/Curiosity)
- **Traje:** O2, bateria, consumo, alcance a pé e tempo de EVA
- **Recursos e navegação:** recarga, O2 e gelo mais próximos, além da análise da rota

**Mapa (M)** com camadas que você pode ligar e desligar:
- Relevo sombreado (MOLA/HiRISE)
- Declividade: vermelho acima de 25° (perigoso), amarelo acima de 15°
- Gelo subsuperficial (SHARAD/SWIM) e minerais hidratados (CRISM)
- Pontos de interesse: habitat, estações de recarga, zona segura, perigos e alvos científicos
- Trajeto percorrido e círculo de alcance da bateria
- **Clique em qualquer ponto** para traçar uma rota. O sistema analisa declive, subida, travessia de áreas perigosas, bateria e O2 de ida e volta, e responde **ROTA SEGURA**, **COM RISCO** ou **NÃO RECOMENDADA**.

O Sol se move de acordo com a latitude e a estação reais. O céu fica caramelo durante o dia e **azul no pôr do sol**, como acontece de verdade em Marte.

## 4. Como personalizar

- **Local:** no componente `MarsEnvironment`, que aparece ao dar Play (ou em `MarsEnvironment.cs`), mude `siteName`, `referenceLatitude`, `referenceLongitude` e `referenceElevation`.
- **Pontos de interesse:** edite a lista `DefaultPOIs` em `MarsBootstrap.cs`. Outra opção é adicionar o componente `PointOfInterest` a qualquer objeto da cena.
- **Valores do traje:** edite `SuitSystems.cs` (consumo, recarga, O2).

## 5. Usar relevo REAL de Marte (opcional, se der tempo)

1. Acesse **NASA Mars Trek**: https://trek.nasa.gov/mars
2. Procure "Jezero" e ative uma camada de elevação (DEM), por exemplo *HRSC/MOLA Blended DEM* ou um *HiRISE DTM*. Use a ferramenta de exportação para baixar o recorte em GeoTIFF.
3. Converta para RAW de 16 bits com 1025×1025 pixels usando GDAL (o QGIS já vem com ele):
   ```
   gdalinfo -mm dem.tif          # anote o mínimo e o máximo de elevação
   gdal_translate -of ENVI -ot UInt16 -scale MIN MAX 0 65535 -outsize 1025 1025 dem.tif mars.raw
   ```
4. No Unity, use **GameObject → 3D Object → Terrain**. Depois vá em Terrain Settings → **Import Raw**, escolha *Depth 16 bit* e *Byte Order Windows*. Em Terrain Size, informe a largura real do recorte e o desnível (máx − mín).
5. Posicione o terreno de modo que o centro fique em (0, y, 0). O `MarsBootstrap` usa automaticamente o Terrain que já estiver na cena.

## 6. Dados e missões da NASA usados como referência

| Dado | Missão / instrumento |
|---|---|
| Relevo | MOLA (Mars Global Surveyor), HiRISE DTM (MRO) |
| Imagens / rochas | HiRISE, CTX (MRO) |
| Minerais | CRISM (MRO) |
| Gelo subsuperficial | SHARAD (MRO), projeto SWIM |
| Inércia térmica / dunas | THEMIS (Mars Odyssey) |
| Clima / poeira | MCS (MRO), REMS (Curiosity), MEDA (Perseverance) |
| Radiação | RAD (Curiosity) |
| Tempo marciano | Mars24 (NASA GISS) |

Os valores ambientais são **modelados** a partir de médias publicadas, sem telemetria ao vivo. Diga isso na apresentação: mostra rigor. O próximo passo natural do projeto seria conectar APIs ou arquivos reais do PDS.

## 7. Roteiro de demo (3 min)

1. Comece andando pela base e mostre o HUD: hora marciana, pressão e o salto em gravidade baixa.
2. Abra o **TAB** e explique de que missão vem cada cartão.
3. Abra o **M** e ligue e desligue as camadas (relevo → declividade → gelo → minerais).
4. Clique no **gelo** para ver a rota com risco (passa pelo campo de rochas). Depois clique na **Estação Solar** para ver uma ROTA SEGURA.
5. Aperte **2** algumas vezes para acelerar o tempo: o pôr do sol azul, a noite e a lanterna (**F**).
6. Aperte **G** para simular a tempestade de poeira: a visibilidade cai, aparecem alertas e a rota deixa de ser segura.
