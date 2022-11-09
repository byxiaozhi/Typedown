const createApplicationMenuState = ({ start, end, affiliation }) => {
  const state = {
    isDisabled: false,
    // Whether multiple lines are selected.
    isMultiline: start.key !== end.key,
    // List information - a list must be selected.
    isLooseListItem: false,
    isTaskList: false,
    // Whether the selection is code block like (math, html or code block).
    isCodeFences: false,
    // Whether a code block line is selected.
    isCodeContent: false,
    // Whether the selection contains a table.
    isTable: false,
    // Contains keys about the selection type(s) (string, boolean) like "ul: true".
    affiliation: {}
  }
  const { isMultiline } = state

  // Get code block information from selection.
  if (
    (start.block.functionType === 'cellContent' && end.block.functionType === 'cellContent') ||
    (start.type === 'span' && start.block.functionType === 'codeContent') ||
    (end.type === 'span' && end.block.functionType === 'codeContent')
  ) {
    // A code block like block is selected (code, math, ...).
    state.isCodeFences = true

    // A code block line is selected.
    if (start.block.functionType === 'codeContent' || end.block.functionType === 'codeContent') {
      state.isCodeContent = true
    }
  }

  // Query list information.
  if (affiliation.length >= 1 && /ul|ol/.test(affiliation[0].type)) {
    const listBlock = affiliation[0]
    state.affiliation[listBlock.type] = true
    state.isLooseListItem = listBlock.children[0].isLooseListItem
    state.isTaskList = listBlock.listType === 'task'
  } else if (affiliation.length >= 3 && affiliation[1].type === 'li') {
    const listItem = affiliation[1]
    const listType = listItem.listItemType === 'order' ? 'ol' : 'ul'
    state.affiliation[listType] = true
    state.isLooseListItem = listItem.isLooseListItem
    state.isTaskList = listItem.listItemType === 'task'
  }

  // Search with block depth 3 (e.g. "ul -> li -> p" where p is the actually paragraph inside the list (item)).
  for (const b of affiliation.slice(0, 3)) {
    if (b.type === 'pre' && b.functionType) {
      if (/frontmatter|html|multiplemath|code$/.test(b.functionType)) {
        state.isCodeFences = true
        state.affiliation[b.functionType] = true
      }
      break
    } else if (b.type === 'figure' && b.functionType) {
      if (b.functionType === 'table') {
        state.isTable = true
        state.isDisabled = true
      }
      break
    } else if (isMultiline && /^h{1,6}$/.test(b.type)) {
      // Multiple block elements are selected.
      state.affiliation = {}
      break
    } else {
      if (!state.affiliation[b.type]) {
        state.affiliation[b.type] = true
      }
    }
  }

  // Clean up
  if (Object.getOwnPropertyNames(state.affiliation).length >= 2 && state.affiliation.p) {
    delete state.affiliation.p
  }
  if ((state.affiliation.ul || state.affiliation.ol) && state.affiliation.li) {
    delete state.affiliation.li
  }
  return state
}

export { createApplicationMenuState };