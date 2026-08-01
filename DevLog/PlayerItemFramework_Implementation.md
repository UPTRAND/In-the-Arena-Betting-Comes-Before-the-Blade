# Player Item Framework Implementation

> **참고:** 이전에 작성된 문서가 `git clean -fd` 과정에서 초기화되어 새롭게 작성되었습니다. 기존의 전체 구현 의도(아이템 구매, 인벤토리 관리, 전투 페이즈 시 아이템 드래그 등)를 포함하여 수정된 아키텍처에 맞게 갱신되었습니다.

## 기존 기능 개요 및 구조
* **목표:** 플레이어가 골드를 사용해 아이템을 구매하고 베팅/전투 페이즈에 적절하게 사용하는 시스템 구축
* **데이터 구조:** `SaveManager.PlayerData` 내부에 아이템 보유 수량을 저장하여 영속성 유지
* **동작 원리:** 라운드 시작 시 `RoundManager`가 `SaveManager`로부터 현재 보유 아이템 수량을 복사(Cache)해오며, 이후의 아이템 소모 로직은 이 임시 배열을 대상으로 이루어집니다. 스테이지를 성공적으로 마칠 때 `SaveManager`에 변경된 수량을 최종 반영합니다.

---

## 검증 과정에서 발견한 문제

1. **아키텍처 및 싱글톤 남용 문제**
   - 기존의 `ItemManager` 방식은 게임 흐름 통제의 주축인 `RoundManager`와 별개의 싱글톤으로 존재하여 구조적 위배가 있었습니다.
   - `SaveManager`에 직접 접근하여 값을 바꾸는 로직은 페이즈 도중 강제 종료될 경우 아이템만 잃게 되는 심각한 결함을 내포했습니다.

2. **UI와 로직의 강결합 문제**
   - 배팅 페이즈(`BettingPhase.cs`)에 버튼 등의 UI 참조 필드가 직접 노출되어 있었으며, 뷰(UI)와 로직(Model)이 강하게 결합되어 있었습니다.
   - 프로젝트 규칙인 `UI_Base`를 상속받은 프리팹 분리 관리가 전혀 이루어지지 않았습니다.

3. **코드상 미구현 및 컴파일 오류**
   - 용병 스폰 과정에서 `UnitData` 대신 잘못된 타입을 인자로 넘겨 컴파일 에러가 발생했습니다. (`AddRuntimeUnit` 부재)
   - 메테오 기절 효과 등 문서상 구현되었다고 한 부분이 실제로는 기절 중첩 버그를 유발할 수 있었고, 드래그 시작 즉시 아이템이 소모되는 등 치명적인 논리 결함이 존재했습니다.
   - 특수 베팅 갱신(리롤) 시 UI에 반영되지 않거나, 전투 연장 로직이 누락되는 등의 문제도 있었습니다.

---

## 발견한 문제를 기반해서 수정한 일련의 과정

### 1. ItemManager 제거 및 아키텍처 재설계
* **의도:** 불필요한 싱글톤을 배제하고 `RoundManager` 중심의 수직적 구조를 확립하기 위함입니다.
* **과정:** `ItemManager.cs`를 삭제하고, `RoundManager` 내부에 `RoundItemCounts` 배열을 도입했습니다. 페이즈 시작 시 `SaveManager` 데이터를 로드하고, `StageManager`가 스테이지 종료 시점에만 `SaveManager`에 저장하도록 변경했습니다. (강제 종료 시 아이템 복구 가능)

### 2. 순수 데이터 클래스 ItemData 도입
* **의도:** 아이템을 식별하고 UI와 로직 사이의 결합도를 낮추기 위함입니다.
* **과정:** `ItemCategory`와 `ItemType` Enum을 가진 `ItemData`를 `ScriptableObject`로 생성했습니다.

### 3. BettingPhase 및 CombatPhase UI 종속성 제거
* **의도:** 게임 매니저는 게임의 논리만 처리하고 시각적 요소는 UI 클래스가 담당하는 MVC 패턴을 준수하기 위함입니다.
* **과정:** `BettingPhase.cs`의 모든 UI 관련 필드(`[SerializeField] private Button ...`)와 함수를 삭제했습니다. 대신 외부 UI에서 호출할 수 있도록 `UseBettingItem(ItemData ...)` 및 `ConfirmBetting()` 함수만 public으로 열어두었습니다. `CombatPhase.cs` 역시 순수하게 `UseCombatItem()` 함수만 추가했습니다.

### 4. UI_Base 기반의 신규 UI 스크립트 작성
* **의도:** UI가 독립된 프리팹으로서 동작하며, 매니저와의 통신은 단방향으로만 일어나게 하기 위함입니다.
* **과정:** `UI_BettingPhase.cs`와 `UI_CombatItemDragController.cs`를 작성하고 `UI_Base`를 상속받게 했습니다. 아이템 사용 시 `RoundManager.Instance.BettingPhase.UseBettingItem(...)` 등을 호출하여 반환값(성공 여부, 안내 메시지, 남은 개수)을 받아 UI만 갱신하게 구현했습니다. 이로써 드래그 즉시 소모되던 버그도 방지했습니다.

### 5. 전투 로직 보완 및 컴파일 에러 해결
* **의도:** 문서가 단정했던 용병 고용이나 메테오 효과가 실제로 동작하게 보장하기 위함입니다.
* **과정:** 
  - `CombatPhase.cs` 내의 용병 생성 로직을 `PoolManager.Require().Units.Spawn(...)`의 올바른 시그니처에 맞게 수정하여(`Vector3` -> `Transform` 등) 컴파일 불가를 해결했습니다.
  - 메테오 기절 효과의 경우 `ApplyDamageAndStun()`을 통해 `StatusEffectData`를 부여하는 형식으로 수정했으며, 람다 및 삼항 연산자를 최소화하고 모든 if문에 중괄호를 작성하는 등 가독성 원칙을 엄격하게 준수했습니다.

### 6. 임시 플레이어 캐시(StagePlayerState) 도입
* **의도:** 스테이지 진행 중(전투, 배팅 등)에 아이템을 획득/소모할 때 SaveManager에 즉시 반영하면 강제 종료 시 악용이나 데이터 오염이 발생할 수 있으므로 안전한 임시 캐시를 사용합니다.
* **과정:** \StagePlayerState.cs\ 클래스를 신설하고, \StageManager\가 이를 소유하게 하였습니다. 스테이지 시작 시 \SaveManager\에서 상태를 복사하고, 스테이지 성공 종료 시에만 다시 \SaveManager\에 덮어쓰도록(Apply) 구현했습니다.

### 7. ItemInventoryService 의존성 주입 및 분리
* **의도:** 정적 유틸리티나 매니저 내부에 아이템 차감 로직이 섞여 있던 것을 서비스 클래스로 분리하여 원자성과 결합도를 낮춥니다.
* **과정:** \ItemInventoryService.cs\를 구현하고, \SaveManager\에 인스턴스를 소유시켜 어디서든 접근 가능하게 하였습니다. 로비에서는 \SaveManager\에 직접 접근해 구매/사용을 반영하고, 스테이지 도중에는 \StagePlayerState\를 조작하게 분기 처리했습니다.

### 8. 정산(BetSettlementService) 및 특수 베팅 연동
* **의도:** 추가 배팅권(500콜) 및 보험(패배 시 원금 반환) 아이템이 실제 게임 정산에 영향을 주도록 연동하고, 리롤 아이템 사용 시 유효한 특수 배팅이 변경되도록 합니다.
* **과정:** \RoundBetTicket\에 \HasAdditionalBet\, \HasInsurance\ 플래그를 추가하고, \BetSettlementService\에서 이를 기반으로 Payout을 보정했습니다. 특수 배팅 리롤 시 \RoundContext.ActiveSpecialBets\를 갱신하고 \OnSpecialBetChanged\ 이벤트를 발생시키도록 처리했습니다.

### 9. 상점 UI(UI_ItemStore) 생성 및 연결
* **의도:** 로비와 인게임에서 아이템을 구매할 수 있는 UI를 MVC 기반으로 분리하여 구현합니다.
* **과정:** \UI_ItemStore.cs\를 신설하여 \StoreType\ 필드를 통해 로비인지 스테이지인지 구분하여 \ItemInventoryService\에 구매 요청을 보내고 결과를 피드백 텍스트와 UI 갱신으로 보여주도록 구현했습니다.

