import SwiftUI
import HangLaterCore

struct TaskActionsMenu<LabelContent: View>: View {
    let task: HangingTask
    @ObservedObject var store: AppStore
    @ViewBuilder let label: () -> LabelContent

    var body: some View {
        Menu {
            Button(store.text("编辑", "Edit")) {
                store.edit(task)
            }
            Button {
                store.editReminder(task)
            } label: {
                Label(task.dueAt == nil
                      ? store.text("添加提醒时间…", "Add reminder…")
                      : store.text("修改提醒时间…", "Change reminder…"),
                      systemImage: "bell")
            }
            TaskTypeMenu(task: task, store: store)
            Divider()
            Button(store.text("删除", "Delete"), role: .destructive) {
                store.remove(task)
            }
        } label: {
            label()
        }
    }
}

struct TaskTypeMenu: View {
    let task: HangingTask
    @ObservedObject var store: AppStore

    var body: some View {
        Menu(store.text("任务类型", "Task type")) {
            Button {
                store.setTaskType(nil, for: task)
            } label: {
                Label(store.text("默认边框", "Default border"),
                      systemImage: task.taskTypeID == nil ? "checkmark" : "circle")
            }
            if !store.taskTypes.isEmpty { Divider() }
            ForEach(store.taskTypes) { type in
                Button {
                    store.setTaskType(type.id, for: task)
                } label: {
                    Label(type.name,
                          systemImage: task.taskTypeID == type.id ? "checkmark" : "circle.fill")
                }
            }
        }
    }
}
