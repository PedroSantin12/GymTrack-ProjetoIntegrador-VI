# Imagens demonstrativas dos exercícios

A biblioteca inicial usa imagens estáticas. As 32 novas ilustrações foram geradas para o GymTrack a partir da referência visual enviada pelo usuário: fundo branco, duas posições lado a lado, anatomia em cinza e grupos musculares destacados em vermelho. São representações educativas; as instruções de execução e cuidados estão na página de cada exercício.

## Ilustrações geradas para o GymTrack

Prompt base usado: “Educational exercise anatomy plate, white landscape canvas, two adjacent views of the same adult athlete in the start and finish positions, grayscale realistic musculature, restrained muted red highlight on the working muscles, accurate equipment and posture, no labels, arrows, logos or watermark.” Cada chamada acrescentou o nome do exercício, equipamento, posições e músculos específicos. Em casos de bloqueio automático, o atleta foi representado com camiseta e calça esportivas opacas. A flexão de punhos foi refeita com os antebraços apoiados e movimento exclusivo dos punhos.

| Exercício | Arquivo |
| --- | --- |
| Prancha frontal | `anatomy_prancha_frontal.png` |
| Abdominal no solo | `anatomy_abdominal_no_solo.png` |
| Flexão de punhos com halteres | `anatomy_flexao_de_punhos_com_halteres.png` |
| Rosca direta com halteres | `anatomy_rosca_direta_com_halteres.png` |
| Rosca martelo | `anatomy_rosca_martelo.png` |
| Remada alta com halteres | `anatomy_remada_alta_com_halteres.png` |
| Puxada frontal | `anatomy_puxada_frontal.png` |
| Remada curvada | `anatomy_remada_curvada.png` |
| Extensão lombar no banco | `anatomy_extensao_lombar_no_banco.png` |
| Desenvolvimento com halteres | `anatomy_desenvolvimento_com_halteres.png` |
| Elevação lateral | `anatomy_elevacao_lateral.png` |
| Supino reto com halteres | `anatomy_supino_reto_com_halteres.png` |
| Supino inclinado com halteres | `anatomy_supino_inclinado_com_halteres.png` |
| Crucifixo com halteres | `anatomy_crucifixo_com_halteres.png` |
| Flexão de braços | `anatomy_flexao_de_bracos.png` |
| Mobilidade cervical | `anatomy_mobilidade_cervical.png` |
| Encolhimento com halteres | `anatomy_encolhimento_com_halteres.png` |
| Tríceps na polia | `anatomy_triceps_na_polia.png` |
| Tríceps francês | `anatomy_triceps_frances.png` |
| Cadeira abdutora | `anatomy_cadeira_abdutora.png` |
| Cadeira adutora | `anatomy_cadeira_adutora.png` |
| Elevação pélvica | `anatomy_elevacao_pelvica.png` |
| Agachamento sumô | `anatomy_agachamento_sumo.png` |
| Coice no cabo | `anatomy_coice_no_cabo.png` |
| Elevação de panturrilhas em pé | `anatomy_elevacao_de_panturrilhas_em_pe.png` |
| Mesa flexora | `anatomy_mesa_flexora.png` |
| Levantamento terra romeno | `anatomy_levantamento_terra_romeno.png` |
| Agachamento livre | `anatomy_agachamento_livre.png` |
| Leg press | `anatomy_leg_press.png` |
| Caminhada na esteira | `anatomy_caminhada_na_esteira.png` |
| Polichinelo | `anatomy_polichinelo.png` |
| Alongamento geral | `anatomy_alongamento_geral.png` |

## Imagem licenciada da referência

| Exercício | Arquivo | Autor / fonte | Licença |
| --- | --- | --- | --- |
| Cadeira extensora | `exercise_leg_extension.jpg` | [Franpol / wger](https://wger.de/api/v2/exerciseimage/386/) | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) |

Os arquivos ficam em `GymTrack/Resources/Images`; o vínculo entre exercício e arquivo está em `GymTrack/Services/ExerciseMediaCatalog.cs`. Exercícios personalizados sem imagem própria continuam sem demonstração atribuída.
