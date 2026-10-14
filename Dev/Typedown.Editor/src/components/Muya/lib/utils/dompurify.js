import DOMPurify from 'dompurify'

export const isValidAttribute = (...args) => DOMPurify.isValidAttribute(...args)

export default (...args) => DOMPurify.sanitize(...args)
