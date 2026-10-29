# FF Battle — chiến đấu bằng menu lệnh

FF Battle là hệ thống chiến đấu mang phong cách RPG máy chơi game cổ điển: đội của bạn ở góc dưới bên trái, kẻ địch ở trên, cùng một menu lệnh — Attack, Items, Defend, Escape. Đây là hệ thống chiến đấu của RPG Plus. Bản thân trận chiến — khởi đầu, nền, màn hình thắng thua, kinh nghiệm và chiến lợi phẩm — do Turn Battle Base đảm nhiệm; còn FF Battle quyết định người chơi đánh thế nào.

## Màn hình chiến đấu

Chiến binh RPG Plus chạm trán một con sói trong làng:

![Màn hình chiến đấu FF Battle: thẻ của con sói ở trên (31/31, ATK 7, DEF 2), thẻ nhỏ của anh hùng dưới trái (42/42, MP 6/6, DEF 4, một điểm hành động màu vàng), bảng Battle trên trái (Round 1, Enemies 1, Party 1), bên phải là các nút tốc độ và nhật ký chiến đấu.](assets/ff-start.jpg)

- **Thẻ.** Mỗi nhân vật cho thấy máu, sát thương và phòng thủ (DEF là lớp khiên hấp thụ sát thương trước), mana nếu game có, và các chấm vàng là điểm hành động (AP).
- **Bảng Battle** — vòng đấu, số kẻ địch và thành viên đội còn lại, và đến lượt của ai.
- **Nhật ký chiến đấu** — ghi lại từng hành động và kết quả của nó, cái mới nhất nằm dưới cùng.

## Diễn biến một lượt

1. **Chọn nhân vật** — nhấp vào thẻ, hoặc nhấn **Enter**/**Space** để chọn nhân vật đầu tiên còn điểm hành động.
2. **Chọn một lệnh** bằng chuột hoặc **W/S**/các phím mũi tên và **Enter**:
   - **Attack** — các hành động chiến đấu của nhân vật;
   - **Items** — vật phẩm dùng được từ túi đồ, mỗi món 1 AP;
   - **Defend** — hành động phòng thủ của nhân vật lên chính nó;
   - **Escape** — tỉ lệ thành công 70%; nếu hụt, nhân vật mất 1 AP.
3. **Chọn hành động.** Bảng cạnh menu sẽ hiện thẻ hành động và hiệu ứng của nó:

![Menu hành động của anh hùng — Basic attack (1P), Defence (1P), Power strike (1P), Back — và thẻ Basic attack kèm gợi ý "Sát thương 12 (ATK 12 × 1,0) · tốn 1 AP".](assets/ff-actions.jpg)

4. **Nhấp vào mục tiêu.** Dòng gợi ý "Chọn mục tiêu · chuột phải / Esc — hủy" xuất hiện và các thẻ địch sáng lên:

![Thẻ của con sói được tô đỏ và dòng gợi ý "Chọn mục tiêu · chuột phải / Esc — hủy".](assets/ff-target.jpg)

Khi đội dùng hết điểm hành động, quân địch sẽ đến lượt hành động; sau đó một vòng mới bắt đầu và điểm được hồi lại.

## Đọc nhật ký

Sau một đòn đánh thường và đòn đáp trả của con sói:

![Nhật ký chiến đấu: Hero: Basic attack → Wolf; Wolf's shield absorbed 2 · left 0; Basic attack: Hero → Wolf · -10 HP; The party is out of AP; Enemy turn · round 1; Wolf: Defence.](assets/ff-log.jpg)

Đòn đánh của anh hùng là 12. Lớp khiên 2 điểm của sói hấp thụ phần đầu rồi cạn, nên con sói mất 10 máu: 31 → 21. Sau đó nó dùng Defence, nâng lớp khiên lên 4 để đỡ đòn tiếp theo.

## Các hành động

Menu liệt kê các hành động mà nhân vật có (xem Characters Engine). Còn các loại cơ bản thì FF Battle tự xử lý:

- `Attack=1.5` — sát thương của đòn đánh nhân với con số; lớp khiên của mục tiêu hấp thụ trước;
- `Defence=4` — đặt lớp khiên lên đồng đội;
- `Turn=2` — khiến mục tiêu choáng đúng số lượt đó;
- dùng vật phẩm.

Các hành động viết bằng công thức, chẳng hạn Power strike hay Fireball của Classic RPG Rules, hoạt động thông qua Battle Formulas; chi phí mana (`Cost=mp:4`) trừ vào mana của nhân vật.

## Khi có sự cố

1. **Menu không mở ra.** Nhân vật đã hết điểm hành động trong vòng này, hoặc đang là lượt của quân địch.
2. **Items bị mờ đi.** Trong túi đồ không còn vật phẩm nào dùng được trong chiến đấu.
3. **Kỹ năng chẳng có tác dụng.** Giá trị của nó cần Battle Formulas, hoặc nó không được đánh dấu là dùng được trong chiến đấu.
